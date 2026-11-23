using System.Diagnostics;
using AutoMapper;
using Library.Catalog.Bll.Caching;
using Library.Catalog.Bll.Dto;
using Library.Catalog.Bll.Interfaces;
using Library.Catalog.Dal.Interfaces;
using Library.Catalog.Dal.Specifications;
using Library.Catalog.Domain.Entities;
using Library.Catalog.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace Library.Catalog.Bll.Services;

/// <summary>
/// Сервіс працює лише через Unit of Work. Правила самої книги (ціна > 0, достатній залишок)
/// перевіряє сутність Book; сервіс — правила, що потребують даних із БД (унікальність ISBN,
/// існування видавця/категорії/авторів). SaveChangesAsync викликається один раз наприкінці операції.
/// </summary>
public class BookService : IBookService
{
    private const int MaxPageSize = 100;

    // Лаб. 05: власні spans бізнес-операцій (джерело зареєстроване в ServiceDefaults через "Library.*")
    private static readonly ActivitySource Source = new("Library.Catalog");

    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly ILogger<BookService> _logger;
    private readonly ITwoLevelCache _cache;

    public BookService(IUnitOfWork uow, IMapper mapper, ILogger<BookService> logger, ITwoLevelCache cache)
    {
        _uow = uow;
        _mapper = mapper;
        _logger = logger;
        _cache = cache;
    }

    public async Task<PagedResult<BookDto>> GetListAsync(BookQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        var spec = new BooksFilterSpec(
            query.Search, query.MinPrice, query.MaxPrice, query.CategoryId, query.PublisherId,
            query.SortBy, query.SortDir, page, pageSize);

        var total = await _uow.Books.CountAsync(spec, ct);      // SELECT COUNT(*) з тими самими фільтрами
        var items = await _uow.Books.ListAsync(spec, ct);       // лише одна сторінка (OFFSET/FETCH)

        return new PagedResult<BookDto>(_mapper.Map<List<BookDto>>(items), total, page, pageSize);
    }

    /// <summary>
    /// Картка книги — read-heavy операція: cache-aside L1 (пам'ять) → L2 (Redis) → БД.
    /// Той самий метод викликають REST-контролер і gRPC-сервіс, тому кеш працює для обох протоколів.
    /// Відсутня книга (EntityNotFoundException) не кешується.
    /// </summary>
    public Task<BookCardDto> GetByIdAsync(long id, CancellationToken ct = default) =>
        _cache.GetOrCreateAsync(CacheKeys.Product(id), CachePolicies.Product, async token =>
        {
            var book = await _uow.Books.GetWithDetailsAsync(id, token)
                       ?? throw new EntityNotFoundException(nameof(Book), id);
            return _mapper.Map<BookCardDto>(book);
        }, ct);

    public async Task<BookCardDto> CreateAsync(CreateBookDto input, CancellationToken ct = default)
    {
        if (await _uow.Books.ExistsByIsbnAsync(input.Isbn, ct))
            throw new ConflictException($"Book with ISBN '{input.Isbn}' already exists.");

        var publisher = await _uow.Publishers.GetByIdAsync(input.PublisherId, ct)
                        ?? throw new EntityNotFoundException(nameof(Publisher), input.PublisherId);
        var category = await _uow.Categories.GetByIdAsync(input.CategoryId, ct)
                       ?? throw new EntityNotFoundException(nameof(Category), input.CategoryId);

        var book = new Book(input.Isbn, input.Title, input.Price, publisher.PublisherId, category.CategoryId, input.AvailableCopies);
        book.SetDetail(input.Annotation, input.PageCount, input.Language);

        foreach (var link in input.Authors)
        {
            var author = await _uow.Authors.GetByIdAsync(link.AuthorId, ct)
                         ?? throw new EntityNotFoundException(nameof(Author), link.AuthorId);
            book.AddAuthor(author, link.Role);
        }

        await _uow.Books.AddAsync(book, ct);
        await _uow.SaveChangesAsync(ct);

        // Нова книга з'являється у списку своєї категорії → список застарів
        await _cache.RemoveAsync(CacheKeys.CategoryBooks(book.CategoryId), "BookCreated", ct);

        return await GetByIdAsync(book.BookId, ct);
    }

    public async Task ChangePriceAsync(long id, decimal newPrice, CancellationToken ct = default)
    {
        using var activity = Source.StartActivity("ChangeBookPrice");
        activity?.SetTag("book.id", id);

        var book = await _uow.Books.GetByIdAsync(id, ct)
                   ?? throw new EntityNotFoundException(nameof(Book), id);

        book.ChangePrice(newPrice);          // правило «ціна > 0» — у сутності
        _uow.Books.Update(book);
        await _uow.SaveChangesAsync(ct);

        await InvalidateBookAsync(book, "BookPriceChanged", ct);   // синхронно з записом — клієнт не отримає застарілу ціну
        _logger.LogInformation("Book {BookId} price changed to {NewPrice}", id, newPrice);
    }

    public async Task WriteOffAsync(long id, int quantity, CancellationToken ct = default)
    {
        using var activity = Source.StartActivity("WriteOffBook");
        activity?.SetTag("book.id", id);
        activity?.SetTag("book.quantity", quantity);

        var book = await _uow.Books.GetByIdAsync(id, ct)
                   ?? throw new EntityNotFoundException(nameof(Book), id);

        book.RemoveCopies(quantity);         // правило «достатній залишок» — у сутності
        _uow.Books.Update(book);
        await _uow.SaveChangesAsync(ct);

        await InvalidateBookAsync(book, "BookWrittenOff", ct);     // змінився AvailableCopies
        _logger.LogInformation("Book {BookId} written off: {Quantity} copies", id, quantity);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var book = await _uow.Books.GetByIdAsync(id, ct)
                   ?? throw new EntityNotFoundException(nameof(Book), id);

        _uow.Books.Delete(book);
        await _uow.SaveChangesAsync(ct);

        await InvalidateBookAsync(book, "BookDeleted", ct);
    }

    /// <summary>Інвалідація пов'язаних ключів: картка книги та список її категорії (обидва рівні кешу).</summary>
    private async Task InvalidateBookAsync(Book book, string reason, CancellationToken ct)
    {
        await _cache.RemoveAsync(CacheKeys.Product(book.BookId), reason, ct);
        await _cache.RemoveAsync(CacheKeys.CategoryBooks(book.CategoryId), reason, ct);
    }

    // Друга специфікація (BooksByCategorySpec) проходить через той самий ListAsync
    // Довідниковий список «книги категорії»: рідко змінюється → абсолютний TTL, вищий пріоритет у L1
    public async Task<IReadOnlyList<BookDto>> GetByCategoryAsync(long categoryId, CancellationToken ct = default) =>
        await _cache.GetOrCreateAsync(CacheKeys.CategoryBooks(categoryId), CachePolicies.CategoryBooks, async token =>
        {
            _ = await _uow.Categories.GetByIdAsync(categoryId, token)
                ?? throw new EntityNotFoundException(nameof(Category), categoryId);

            var books = await _uow.Books.ListAsync(new BooksByCategorySpec(categoryId), token);
            return _mapper.Map<List<BookDto>>(books);
        }, ct);
}

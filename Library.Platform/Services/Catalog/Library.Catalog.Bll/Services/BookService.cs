using System.Diagnostics;
using AutoMapper;
using Library.Catalog.Bll.Caching;
using Library.Catalog.Bll.Dto;
using Library.Catalog.Bll.Interfaces;
using Library.Catalog.Bll.Telemetry;
using Library.Catalog.Dal.Interfaces;
using Library.Catalog.Dal.Specifications;
using Library.Catalog.Domain.Entities;
using Library.Catalog.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Platform.Messaging.Events;
using Platform.Messaging.Publishing;
using Platform.Messaging.Topology;

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
    private readonly CatalogMetrics _metrics;
    private readonly IEventPublisher _publisher;

    public BookService(IUnitOfWork uow, IMapper mapper, ILogger<BookService> logger, ITwoLevelCache cache, CatalogMetrics metrics,
        IEventPublisher publisher)
    {
        _metrics = metrics;
        _publisher = publisher;
        _uow = uow;
        _mapper = mapper;
        _logger = logger;
        _cache = cache;
    }

    public Task<PagedResult<BookDto>> GetListAsync(BookQuery query, CancellationToken ct = default) =>
        _metrics.MeasureAsync("list", () => GetListCoreAsync(query, ct));

    private async Task<PagedResult<BookDto>> GetListCoreAsync(BookQuery query, CancellationToken ct)
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
        _metrics.MeasureAsync("get_card", () => GetCardAsync(id, ct));

    // Без вимірювання: викликається і з GetByIdAsync, і з CreateAsync (щоб не рахувати одну операцію двічі)
    private Task<BookCardDto> GetCardAsync(long id, CancellationToken ct) =>
        _cache.GetOrCreateAsync(CacheKeys.Product(id), CachePolicies.Product, async token =>
        {
            var book = await _uow.Books.GetWithDetailsAsync(id, token)
                       ?? throw new EntityNotFoundException(nameof(Book), id);
            return _mapper.Map<BookCardDto>(book);
        }, ct);

    public Task<BookCardDto> CreateAsync(CreateBookDto input, CancellationToken ct = default) =>
        _metrics.MeasureAsync("create", () => CreateCoreAsync(input, ct));

    private async Task<BookCardDto> CreateCoreAsync(CreateBookDto input, CancellationToken ct)
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

        return await GetCardAsync(book.BookId, ct);
    }

    public Task ChangePriceAsync(long id, decimal newPrice, CancellationToken ct = default) =>
        _metrics.MeasureAsync("change_price", () => ChangePriceCoreAsync(id, newPrice, ct));

    private async Task ChangePriceCoreAsync(long id, decimal newPrice, CancellationToken ct)
    {
        using var activity = Source.StartActivity("ChangeBookPrice");
        activity?.SetTag("book.id", id);

        var book = await _uow.Books.GetByIdAsync(id, ct)
                   ?? throw new EntityNotFoundException(nameof(Book), id);

        var oldPrice = book.Price;
        book.ChangePrice(newPrice);          // правило «ціна > 0» — у сутності
        _uow.Books.Update(book);
        await _uow.SaveChangesAsync(ct);     // 1) зміни збережено в БД

        await InvalidateBookAsync(book, "BookPriceChanged", ct);   // синхронно з записом — клієнт не отримає застарілу ціну
        _logger.LogInformation("Book {BookId} price changed to {NewPrice}", id, newPrice);

        // 2) лише тепер — подія (якщо SaveChanges кинув виняток, до цього місця виконання не доходить)
        if (oldPrice != newPrice)
            await PublishAsync(new BookPriceChangedEvent(book.BookId, book.Title, oldPrice, newPrice),
                CatalogRoutingKeys.PriceChanged, ct);
    }

    public Task RenameAsync(long id, string newTitle, CancellationToken ct = default) =>
        _metrics.MeasureAsync("rename", () => RenameCoreAsync(id, newTitle, ct));

    private async Task RenameCoreAsync(long id, string newTitle, CancellationToken ct)
    {
        using var activity = Source.StartActivity("RenameBook");
        activity?.SetTag("book.id", id);

        var book = await _uow.Books.GetByIdAsync(id, ct)
                   ?? throw new EntityNotFoundException(nameof(Book), id);

        var oldTitle = book.Title;
        book.Rename(newTitle);
        _uow.Books.Update(book);
        await _uow.SaveChangesAsync(ct);     // 1) зміни збережено в БД

        await InvalidateBookAsync(book, "BookRenamed", ct);
        _logger.LogInformation("Book {BookId} renamed from '{OldTitle}' to '{NewTitle}'", id, oldTitle, book.Title);

        if (oldTitle != book.Title)          // 2) подія — після успішного збереження
            await PublishAsync(new BookRenamedEvent(book.BookId, oldTitle, book.Title), CatalogRoutingKeys.Renamed, ct);
    }

    /// <summary>
    /// Публікація інтеграційної події ПІСЛЯ збереження. Збій брокера не скасовує вже збережену зміну (HTTP-клієнт
    /// отримує успіх), але фіксується як Error: це саме те вікно, яке закриває Transactional Outbox
    /// (подія записується в таблицю тієї ж БД у тій самій транзакції; окремий процес публікує її) — див. README.
    /// </summary>
    private async Task PublishAsync<TEvent>(TEvent @event, string routingKey, CancellationToken ct) where TEvent : IntegrationEvent
    {
        try
        {
            await _publisher.PublishAsync(@event, MessagingTopology.CatalogExchange, routingKey, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Event {EventType} {EventId} was NOT published (change already saved); consumers' copies are stale until reconciled",
                typeof(TEvent).Name, @event.EventId);
        }
    }

    public Task WriteOffAsync(long id, int quantity, CancellationToken ct = default) =>
        _metrics.MeasureAsync("write_off", () => WriteOffCoreAsync(id, quantity, ct));

    private async Task WriteOffCoreAsync(long id, int quantity, CancellationToken ct)
    {
        using var activity = Source.StartActivity("WriteOffBook");
        activity?.SetTag("book.id", id);
        activity?.SetTag("book.quantity", quantity);

        var book = await _uow.Books.GetByIdAsync(id, ct)
                   ?? throw new EntityNotFoundException(nameof(Book), id);

        book.RemoveCopies(quantity);         // правило «достатній залишок» — у сутності
        _uow.Books.Update(book);
        await _uow.SaveChangesAsync(ct);

        _metrics.CopiesWrittenOff(quantity);
        await InvalidateBookAsync(book, "BookWrittenOff", ct);     // змінився AvailableCopies
        _logger.LogInformation("Book {BookId} written off: {Quantity} copies", id, quantity);
    }

    public Task DeleteAsync(long id, CancellationToken ct = default) =>
        _metrics.MeasureAsync("delete", () => DeleteCoreAsync(id, ct));

    private async Task DeleteCoreAsync(long id, CancellationToken ct)
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
    public Task<IReadOnlyList<BookDto>> GetByCategoryAsync(long categoryId, CancellationToken ct = default) =>
        _metrics.MeasureAsync("by_category", () => GetByCategoryCoreAsync(categoryId, ct));

    private async Task<IReadOnlyList<BookDto>> GetByCategoryCoreAsync(long categoryId, CancellationToken ct) =>
        await _cache.GetOrCreateAsync(CacheKeys.CategoryBooks(categoryId), CachePolicies.CategoryBooks, async token =>
        {
            _ = await _uow.Categories.GetByIdAsync(categoryId, token)
                ?? throw new EntityNotFoundException(nameof(Category), categoryId);

            var books = await _uow.Books.ListAsync(new BooksByCategorySpec(categoryId), token);
            return _mapper.Map<List<BookDto>>(books);
        }, ct);
}

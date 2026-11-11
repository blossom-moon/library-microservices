using AutoMapper;
using Library.Catalog.Bll.Dto;
using Library.Catalog.Bll.Interfaces;
using Library.Catalog.Dal.Interfaces;
using Library.Catalog.Domain.Entities;
using Library.Catalog.Domain.Exceptions;

namespace Library.Catalog.Bll.Services;

public class AuthorService : IAuthorService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public AuthorService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<AuthorDto>> GetAllAsync(CancellationToken ct = default)
        => _mapper.Map<List<AuthorDto>>(await _uow.Authors.GetAllAsync(ct));

    // Explicit Loading: два SQL-запити (автор, потім його книги)
    public async Task<AuthorWithBooksDto> GetWithBooksAsync(long id, CancellationToken ct = default)
    {
        var author = await _uow.Authors.GetWithBooksAsync(id, ct)
                     ?? throw new EntityNotFoundException(nameof(Author), id);
        return _mapper.Map<AuthorWithBooksDto>(author);
    }

    // LINQ to Entities через проміжну сутність BookAuthor (поле AddedAt)
    public async Task<IReadOnlyList<BookShortDto>> GetBooksAddedAfterAsync(
        long authorId, DateTime since, CancellationToken ct = default)
    {
        _ = await _uow.Authors.GetByIdAsync(authorId, ct)
            ?? throw new EntityNotFoundException(nameof(Author), authorId);

        var books = await _uow.BookAuthors.GetBooksByAuthorAddedAfterAsync(authorId, since, ct);
        return _mapper.Map<List<BookShortDto>>(books);
    }
}

using Library.Catalog.Bll.Dto;

namespace Library.Catalog.Bll.Interfaces;

public interface IAuthorService
{
    Task<IReadOnlyList<AuthorDto>> GetAllAsync(CancellationToken ct = default);
    Task<AuthorWithBooksDto> GetWithBooksAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<BookShortDto>> GetBooksAddedAfterAsync(long authorId, DateTime since, CancellationToken ct = default);
}

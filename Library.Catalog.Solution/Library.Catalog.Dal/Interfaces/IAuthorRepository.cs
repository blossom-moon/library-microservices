using Library.Catalog.Domain.Entities;

namespace Library.Catalog.Dal.Interfaces;

public interface IAuthorRepository : IGenericRepository<Author>
{
    /// <summary>Explicit Loading: автор — 1-й запит, його книги — окремий 2-й запит.</summary>
    Task<Author?> GetWithBooksAsync(long id, CancellationToken ct = default);
}

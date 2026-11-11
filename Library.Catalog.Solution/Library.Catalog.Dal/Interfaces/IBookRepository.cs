using Library.Catalog.Domain.Entities;

namespace Library.Catalog.Dal.Interfaces;

public interface IBookRepository : IGenericRepository<Book>
{
    /// <summary>Eager Loading: картка книги з усім пов'язаним одним запитом.</summary>
    Task<Book?> GetWithDetailsAsync(long id, CancellationToken ct = default);

    Task<bool> ExistsByIsbnAsync(string isbn, CancellationToken ct = default);
}

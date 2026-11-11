using Library.Catalog.Domain.Entities;

namespace Library.Catalog.Dal.Interfaces;

public interface IBookAuthorRepository : IGenericRepository<BookAuthor>
{
    /// <summary>LINQ to Entities: книги автора, додані після дати, через проміжну сутність і її поле AddedAt.</summary>
    Task<IReadOnlyList<Book>> GetBooksByAuthorAddedAfterAsync(long authorId, DateTime since, CancellationToken ct = default);
}

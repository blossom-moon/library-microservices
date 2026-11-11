using Library.Catalog.Dal.Interfaces;
using Library.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Library.Catalog.Dal.Repositories;

public class BookAuthorRepository : GenericRepository<BookAuthor>, IBookAuthorRepository
{
    public BookAuthorRepository(CatalogDbContext context) : base(context) { }

    // Складений ключ (BookId, AuthorId): пошук за одним Id не має сенсу
    public override Task<BookAuthor?> GetByIdAsync(long id, CancellationToken ct = default)
        => throw new NotSupportedException("BookAuthor has a composite key (BookId, AuthorId).");

    // LINQ to Entities: M:N через проміжну сутність і її власне поле AddedAt
    public async Task<IReadOnlyList<Book>> GetBooksByAuthorAddedAfterAsync(
        long authorId, DateTime since, CancellationToken ct = default)
        => await Set
            .Where(ba => ba.AuthorId == authorId && ba.AddedAt >= since)
            .OrderByDescending(ba => ba.AddedAt)
            .Select(ba => ba.Book)
            .AsNoTracking()
            .ToListAsync(ct);
}

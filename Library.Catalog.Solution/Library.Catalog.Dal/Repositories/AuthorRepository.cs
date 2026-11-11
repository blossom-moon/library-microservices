using Library.Catalog.Dal.Interfaces;
using Library.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Library.Catalog.Dal.Repositories;

public class AuthorRepository : GenericRepository<Author>, IAuthorRepository
{
    public AuthorRepository(CatalogDbContext context) : base(context) { }

    // Explicit Loading — книги автора довантажуються окремим запитом, лише коли потрібні
    public async Task<Author?> GetWithBooksAsync(long id, CancellationToken ct = default)
    {
        var author = await Set.FirstOrDefaultAsync(a => a.AuthorId == id, ct);      // 1-й запит
        if (author is null) return null;

        await Context.Entry(author)
            .Collection(a => a.BookAuthors)
            .Query()
            .Include(ba => ba.Book)
            .LoadAsync(ct);                                                          // 2-й запит
        return author;
    }
}

using Library.Catalog.Dal.Interfaces;
using Library.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Library.Catalog.Dal.Repositories;

public class BookRepository : GenericRepository<Book>, IBookRepository
{
    public BookRepository(CatalogDbContext context) : base(context) { }

    // Eager Loading — один SQL-запит із JOIN (Include / ThenInclude)
    public Task<Book?> GetWithDetailsAsync(long id, CancellationToken ct = default)
        => Set.AsNoTracking()
              .Include(b => b.Detail)
              .Include(b => b.Publisher)
              .Include(b => b.Category)
              .Include(b => b.BookAuthors).ThenInclude(ba => ba.Author)
              .FirstOrDefaultAsync(b => b.BookId == id, ct);

    public Task<bool> ExistsByIsbnAsync(string isbn, CancellationToken ct = default)
        => Set.AnyAsync(b => b.Isbn == isbn, ct);
}

using Library.Catalog.Dal.Interfaces;
using Library.Catalog.Domain.Entities;

namespace Library.Catalog.Dal.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly CatalogDbContext _context;

    public UnitOfWork(CatalogDbContext context)
    {
        _context = context;
        Books = new BookRepository(context);
        Authors = new AuthorRepository(context);
        BookAuthors = new BookAuthorRepository(context);
        Publishers = new GenericRepository<Publisher>(context);
        Categories = new GenericRepository<Category>(context);
    }

    public IBookRepository Books { get; }
    public IAuthorRepository Authors { get; }
    public IBookAuthorRepository BookAuthors { get; }
    public IGenericRepository<Publisher> Publishers { get; }
    public IGenericRepository<Category> Categories { get; }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}

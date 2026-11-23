using Library.Catalog.Domain.Entities;

namespace Library.Catalog.Dal.Interfaces;

public interface IUnitOfWork
{
    IBookRepository Books { get; }
    IAuthorRepository Authors { get; }
    IBookAuthorRepository BookAuthors { get; }
    IGenericRepository<Publisher> Publishers { get; }
    IGenericRepository<Category> Categories { get; }

    /// <summary>Єдине збереження всіх змін бізнес-операції в одній транзакції.</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

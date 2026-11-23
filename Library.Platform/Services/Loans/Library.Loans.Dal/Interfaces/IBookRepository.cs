using Library.Loans.Domain;

namespace Library.Loans.Dal.Interfaces;

public interface IBookRepository : IGenericRepository<Book>
{
    Task<bool> AreAllAvailableAsync(IEnumerable<long> bookIds, CancellationToken ct);
}

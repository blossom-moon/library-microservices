using System.Data;

namespace Library.Loans.Dal.Interfaces;

public interface IUnitOfWork : IAsyncDisposable
{
    IReaderRepository Readers { get; }
    IBookRepository Books { get; }
    ILoanRepository Loans { get; }

    Task BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken ct);
    Task CommitAsync(CancellationToken ct);
    Task RollbackAsync(CancellationToken ct);
}

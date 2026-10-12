using Library.Loans.Domain;

namespace Library.Loans.Dal.Interfaces;

public interface ILoanRepository : IGenericRepository<Loan>
{
    Task<long> CreateLoanAsync(long readerId, DateTime dueDate, IEnumerable<long> bookIds, CancellationToken ct);
    Task ReturnBookAsync(long loanId, long bookId, CancellationToken ct);
    Task CancelLoanAsync(long loanId, CancellationToken ct);
    Task<IEnumerable<Loan>> GetActiveLoansByReaderAsync(long readerId, CancellationToken ct);
    Task<IEnumerable<LoanItem>> GetItemsByLoanIdAsync(long loanId, CancellationToken ct);

    /// <summary>
    /// Multi-mapping: один SQL-запит (JOIN Loans + LoanItems), один Dapper-виклик
    /// QueryAsync&lt;Loan, LoanItem, Loan&gt; зі splitOn — замість двох окремих
    /// запитів (Loan і його LoanItems).
    /// </summary>
    Task<Loan?> GetWithItemsAsync(long loanId, CancellationToken ct);
}

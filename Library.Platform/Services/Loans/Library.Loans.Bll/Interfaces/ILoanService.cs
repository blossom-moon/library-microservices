using Library.Loans.Bll.Dto;

namespace Library.Loans.Bll.Interfaces;

public interface ILoanService
{
    Task<LoanDto> CreateLoanAsync(CreateLoanDto dto, CancellationToken ct);
    Task ReturnBookAsync(long loanId, long bookId, CancellationToken ct);
    Task CancelLoanAsync(long loanId, CancellationToken ct);
    Task<LoanDto> GetByIdAsync(long loanId, CancellationToken ct);
    Task<IEnumerable<LoanDto>> GetActiveLoansByReaderAsync(long readerId, CancellationToken ct);
}
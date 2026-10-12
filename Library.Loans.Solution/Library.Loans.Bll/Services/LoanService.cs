using System.Data;
using AutoMapper;
using Library.Loans.Bll.Dto;
using Library.Loans.Bll.Interfaces;
using Library.Loans.Dal.Interfaces;
using Library.Loans.Domain;

namespace Library.Loans.Bll.Services;

public class LoanService : ILoanService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public LoanService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<LoanDto> CreateLoanAsync(CreateLoanDto dto, CancellationToken ct)
    {
        // Валідація (базова валідація полів — через DataAnnotations на DTO,
        // на рівні API; тут — бізнес-валідація, якій потрібен контекст)
        if (dto.BookIds == null || dto.BookIds.Count == 0)
            throw new ValidationException("At least one book must be selected.");
        if (dto.DueDate <= DateTime.UtcNow.Date)
            throw new ValidationException("DueDate must be in the future.");

        await _uow.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            // Бізнес-інваріант: читач має існувати
            var reader = await _uow.Readers.GetByIdAsync(dto.ReaderId, ct)
                         ?? throw new NotFoundException($"Reader {dto.ReaderId} not found.");

            // Бізнес-інваріант: усі книги мають бути доступні
            var allAvailable = await _uow.Books.AreAllAvailableAsync(dto.BookIds, ct);
            if (!allAvailable)
                throw new BusinessConflictException("One or more books are not available.");

            var loanId = await _uow.Loans.CreateLoanAsync(dto.ReaderId, dto.DueDate, dto.BookIds, ct);
            await _uow.CommitAsync(ct);

            return await GetByIdAsync(loanId, ct);
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }
    }

    public async Task ReturnBookAsync(long loanId, long bookId, CancellationToken ct)
    {
        await _uow.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            // Попередня перевірка в сервісі — швидкий і зрозумілий виняток без
            // походу в процедуру; остаточний арбітр усе одно sp_ReturnBook:
            // вона перевіряє статус під UPDLOCK і відмовляє керованою помилкою
            // (THROW 50002/50003), якщо між перевіркою і запитом хтось встиг
            // змінити стан видачі (захист від стану гонитви).
            var loan = await _uow.Loans.GetByIdAsync(loanId, ct)
                       ?? throw new NotFoundException($"Loan {loanId} not found.");

            if (loan.Status is "Returned" or "Cancelled")
                throw new BusinessConflictException($"Loan {loanId} is already {loan.Status}.");

            await _uow.Loans.ReturnBookAsync(loanId, bookId, ct);
            await _uow.CommitAsync(ct);
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }
    }

    public async Task CancelLoanAsync(long loanId, CancellationToken ct)
    {
        await _uow.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            var loan = await _uow.Loans.GetByIdAsync(loanId, ct)
                       ?? throw new NotFoundException($"Loan {loanId} not found.");

            if (loan.Status is "Returned" or "Cancelled")
                throw new BusinessConflictException($"Loan {loanId} cannot be cancelled (status: {loan.Status}).");

            await _uow.Loans.CancelLoanAsync(loanId, ct);
            await _uow.CommitAsync(ct);
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<LoanDto> GetByIdAsync(long loanId, CancellationToken ct)
    {
        // Multi-mapping: Loan + LoanItems одним JOIN-запитом (ILoanRepository.GetWithItemsAsync)
        var loan = await _uow.Loans.GetWithItemsAsync(loanId, ct)
                   ?? throw new NotFoundException($"Loan {loanId} not found.");

        var dto = _mapper.Map<LoanDto>(loan);
        dto.Items = _mapper.Map<List<LoanItemDto>>(loan.Items);
        return dto;
    }

    public async Task<IEnumerable<LoanDto>> GetActiveLoansByReaderAsync(long readerId, CancellationToken ct)
    {
        var loans = await _uow.Loans.GetActiveLoansByReaderAsync(readerId, ct);
        return _mapper.Map<IEnumerable<LoanDto>>(loans);
    }
}

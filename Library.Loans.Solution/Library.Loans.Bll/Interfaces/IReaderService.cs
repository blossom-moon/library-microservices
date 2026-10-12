using Library.Loans.Bll.Dto;

namespace Library.Loans.Bll.Interfaces;

public interface IReaderService
{
    Task<ReaderDto> GetByIdAsync(long readerId, CancellationToken ct);
    Task<ReaderDto> CreateAsync(CreateReaderDto dto, CancellationToken ct);
}
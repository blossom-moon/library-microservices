using Library.Loans.Domain;

namespace Library.Loans.Dal.Interfaces;

public interface IReaderRepository
{
    Task<Reader?> GetByIdAsync(long readerId, CancellationToken ct);
    Task<Reader?> GetByCardNumberAsync(string cardNumber, CancellationToken ct);
    Task<long> CreateAsync(Reader reader, CancellationToken ct);
    Task UpdateAsync(Reader reader, CancellationToken ct);
    Task SoftDeleteAsync(long readerId, string updatedBy, CancellationToken ct);
}
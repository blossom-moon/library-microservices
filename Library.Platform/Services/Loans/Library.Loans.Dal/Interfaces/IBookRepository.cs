using Library.Loans.Domain;

namespace Library.Loans.Dal.Interfaces;

public interface IBookRepository : IGenericRepository<Book>
{
    Task<bool> AreAllAvailableAsync(IEnumerable<long> bookIds, CancellationToken ct);

    /// <summary>Оновлює ціну локальної копії, якщо подія новіша за вже застосовану (захист від out-of-order). false — нічого не змінено.</summary>
    Task<bool> UpdatePriceAsync(long bookId, decimal price, DateTime occurredAt, CancellationToken ct);

    /// <summary>Оновлює назву локальної копії (Books.Title), якщо подія новіша за вже застосовану. Знімки LoanItems.BookTitle не змінюються.</summary>
    Task<bool> UpdateTitleAsync(long bookId, string title, DateTime occurredAt, CancellationToken ct);
}

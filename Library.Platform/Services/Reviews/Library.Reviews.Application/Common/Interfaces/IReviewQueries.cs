using Library.Reviews.Application.Common.Models;
using Library.Reviews.Application.Reviews;

namespace Library.Reviews.Application.Common.Interfaces;

/// <summary>
/// Контракт читання відгуків (CQRS, сторона Query). Обробники запитів звертаються лише до
/// цього інтерфейсу, а не до IMongoCollection; реалізація — в Infrastructure (проєкції, $match, skip/limit).
/// </summary>
public interface IReviewQueries
{
    Task<ReviewDto?> GetByIdAsync(string id, CancellationToken ct);

    Task<PagedResult<ReviewDto>> GetListAsync(long? bookId, int page, int pageSize, CancellationToken ct);
}

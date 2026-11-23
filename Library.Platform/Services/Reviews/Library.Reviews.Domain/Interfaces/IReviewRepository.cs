using Library.Reviews.Domain.Entities;
using MongoDB.Bson;

namespace Library.Reviews.Domain.Interfaces;

/// <summary>Контракт запису/читання агрегата Review; реалізація — в Infrastructure.</summary>
public interface IReviewRepository
{
    Task<Review?> GetByIdAsync(ObjectId id, CancellationToken ct);
    Task<bool> ExistsAsync(long bookId, string authorId, CancellationToken ct);
    Task AddAsync(Review review, CancellationToken ct);

    /// <summary>Оновлює документ лише якщо його версія збігається; інакше ConflictException.</summary>
    Task UpdateAsync(Review review, CancellationToken ct);

    Task<bool> DeleteAsync(ObjectId id, CancellationToken ct);
}

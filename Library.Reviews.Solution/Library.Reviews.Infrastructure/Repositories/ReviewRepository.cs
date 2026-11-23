using Library.Reviews.Domain.Entities;
using Library.Reviews.Domain.Exceptions;
using Library.Reviews.Domain.Interfaces;
using Library.Reviews.Infrastructure.Persistence;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Library.Reviews.Infrastructure.Repositories;

public class ReviewRepository : IReviewRepository
{
    private readonly IMongoCollection<Review> _reviews;

    public ReviewRepository(MongoDbContext context) => _reviews = context.Reviews;

    public async Task<Review?> GetByIdAsync(ObjectId id, CancellationToken ct)
        => await _reviews.Find(r => r.Id == id).FirstOrDefaultAsync(ct);

    public Task<bool> ExistsAsync(long bookId, string authorId, CancellationToken ct)
        => _reviews.Find(Builders<Review>.Filter.And(
                Builders<Review>.Filter.Eq(r => r.BookId, bookId),
                Builders<Review>.Filter.Eq("author.userId", authorId)))
            .AnyAsync(ct);

    public Task AddAsync(Review review, CancellationToken ct)
        => _reviews.InsertOneAsync(review, cancellationToken: ct);

    public async Task UpdateAsync(Review review, CancellationToken ct)
    {
        var expectedVersion = review.Version;
        review.BumpVersion();

        // Оптимістична конкурентність: замінюємо документ лише з очікуваною версією
        var filter = Builders<Review>.Filter.And(
            Builders<Review>.Filter.Eq(r => r.Id, review.Id),
            Builders<Review>.Filter.Eq(r => r.Version, expectedVersion));

        var result = await _reviews.ReplaceOneAsync(filter, review, cancellationToken: ct);

        if (result.MatchedCount == 0)
            throw new ConflictException(
                $"Review {review.Id} was modified by another request (expected version {expectedVersion}).");
    }

    public async Task<bool> DeleteAsync(ObjectId id, CancellationToken ct)
    {
        var result = await _reviews.DeleteOneAsync(r => r.Id == id, ct);
        return result.DeletedCount > 0;
    }
}

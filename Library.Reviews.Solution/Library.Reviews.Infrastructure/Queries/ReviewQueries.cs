using Library.Reviews.Application.Common.Interfaces;
using Library.Reviews.Application.Common.Models;
using Library.Reviews.Application.Reviews;
using Library.Reviews.Infrastructure.Persistence;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Library.Reviews.Infrastructure.Queries;

/// <summary>
/// Сторона читання CQRS: проєкція прямо в DTO на боці MongoDB ($project), без завантаження
/// повної сутності; фільтрація $match, пагінація $skip/$limit.
/// </summary>
public class ReviewQueries : IReviewQueries
{
    private readonly IMongoCollection<BsonDocument> _docs;

    public ReviewQueries(MongoDbContext context)
        => _docs = context.Database.GetCollection<BsonDocument>(context.ReviewsCollectionName);

    private static readonly BsonDocument Projection = new BsonDocument
    {
        { "_id", 1 },
        { "bookId", 1 },
        { "bookTitle", 1 },
        { "authorName", "$author.displayName" },
        { "rating", 1 },
        { "text", 1 },
        { "version", 1 },
        { "commentsCount", new BsonDocument("$size", new BsonDocument("$ifNull", new BsonArray { "$comments", new BsonArray() })) }
    };

    public async Task<ReviewDto?> GetByIdAsync(string id, CancellationToken ct)
    {
        if (!ObjectId.TryParse(id, out var oid)) return null;

        var doc = await _docs.Aggregate()
            .Match(new BsonDocument("_id", oid))
            .Project<BsonDocument>(Projection)
            .FirstOrDefaultAsync(ct);

        return doc is null ? null : ToDto(doc);
    }

    public async Task<PagedResult<ReviewDto>> GetListAsync(long? bookId, int page, int pageSize, CancellationToken ct)
    {
        var match = bookId.HasValue ? new BsonDocument("bookId", bookId.Value) : new BsonDocument();

        var total = await _docs.CountDocumentsAsync(match, cancellationToken: ct);

        var docs = await _docs.Aggregate()
            .Match(match)
            .Sort(new BsonDocument { { "createdAt", -1 }, { "_id", -1 } })
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .Project<BsonDocument>(Projection)
            .ToListAsync(ct);

        return new PagedResult<ReviewDto>(docs.Select(ToDto).ToList(), (int)total, page, pageSize);
    }

    private static ReviewDto ToDto(BsonDocument d) => new()
    {
        Id = d["_id"].AsObjectId.ToString(),
        BookId = d["bookId"].ToInt64(),
        BookTitle = d.GetValue("bookTitle", "").AsString,
        AuthorName = d.GetValue("authorName", "").AsString,
        Rating = d["rating"].ToInt32(),
        Text = d.GetValue("text", "").AsString,
        CommentsCount = d.GetValue("commentsCount", 0).ToInt32(),
        Version = d.GetValue("version", 0).ToInt32()
    };
}

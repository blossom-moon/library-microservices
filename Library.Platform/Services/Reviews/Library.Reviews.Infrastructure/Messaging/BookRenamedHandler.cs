using Library.Reviews.Domain.Entities;
using Library.Reviews.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Platform.Messaging.Consuming;
using Platform.Messaging.Events;

namespace Library.Reviews.Infrastructure.Messaging;

/// <summary>
/// Оновлює денормалізовану назву книги (bookTitle) в усіх відгуках про неї (політика дублювання Лаб. 01).
/// Ідемпотентність: перед обробкою перевіряється запис у processedEvents (_id = EventId); сама зміна теж
/// ідемпотентна (SET за умови, що подія новіша за застосовану), тому порядок «застосувати → записати EventId» безпечний
/// навіть без багатодокументних транзакцій (автономний MongoDB їх не підтримує): збій між кроками призведе лише
/// до безпечного повторного застосування тієї ж зміни.
/// </summary>
public class BookRenamedHandler(MongoDbContext db, ILogger<BookRenamedHandler> logger) : IEventHandler<BookRenamedEvent>
{
    public async Task<HandlingResult> HandleAsync(BookRenamedEvent e, CancellationToken ct)
    {
        var eventKey = e.EventId.ToString();

        if (await db.ProcessedEvents.Find(Builders<BsonDocument>.Filter.Eq("_id", eventKey)).AnyAsync(ct))
        {
            logger.LogInformation("Duplicate event {EventId} skipped (BookId {BookId})", e.EventId, e.BookId);
            return HandlingResult.Duplicate;
        }

        var filter = Builders<Review>.Filter.And(
            Builders<Review>.Filter.Eq(r => r.BookId, e.BookId),
            Builders<Review>.Filter.Or(
                Builders<Review>.Filter.Eq(r => r.BookTitleSyncedAt, null),
                Builders<Review>.Filter.Lt(r => r.BookTitleSyncedAt, e.OccurredAt)));

        var update = Builders<Review>.Update
            .Set(r => r.BookTitle, e.NewTitle)
            .Set(r => r.BookTitleSyncedAt, e.OccurredAt)
            .Set(r => r.UpdatedAt, DateTime.UtcNow)
            .Inc(r => r.Version, 1);   // конкурентне редагування відгуку (ReplaceOne за версією) не затре нову назву

        var result = await db.Reviews.UpdateManyAsync(filter, update, cancellationToken: ct);

        try
        {
            await db.ProcessedEvents.InsertOneAsync(
                new BsonDocument { { "_id", eventKey }, { "eventType", nameof(BookRenamedEvent) }, { "processedAt", DateTime.UtcNow } },
                cancellationToken: ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // паралельна обробка того самого EventId — зміна ідемпотентна, помилки немає
        }

        logger.LogInformation("Book {BookId} renamed to '{NewTitle}' in reviews: matched {Matched}, modified {Modified} (event {EventId})",
            e.BookId, e.NewTitle, result.MatchedCount, result.ModifiedCount, e.EventId);

        return result.ModifiedCount > 0 ? HandlingResult.Processed : HandlingResult.Skipped;
    }
}

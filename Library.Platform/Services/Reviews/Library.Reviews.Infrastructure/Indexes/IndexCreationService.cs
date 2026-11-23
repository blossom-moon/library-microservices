using Library.Reviews.Infrastructure.Persistence;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Library.Reviews.Infrastructure.Indexes;

/// <summary>Створює індекси при старті (CreateManyAsync ідемпотентний для однакових визначень).</summary>
public class IndexCreationService : IHostedService
{
    private readonly MongoDbContext _context;
    private readonly ILogger<IndexCreationService> _logger;

    public IndexCreationService(MongoDbContext context, ILogger<IndexCreationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            var keys = Builders<Domain.Entities.Review>.IndexKeys;

            await _context.Reviews.Indexes.CreateManyAsync(new[]
            {
                // складений: відгуки книги, найновіші першими
                new CreateIndexModel<Domain.Entities.Review>(
                    keys.Ascending("bookId").Descending("createdAt"),
                    new CreateIndexOptions { Name = "ix_bookId_createdAt" }),

                // повнотекстовий пошук
                new CreateIndexModel<Domain.Entities.Review>(
                    keys.Text("text").Text("bookTitle"),
                    new CreateIndexOptions { Name = "ix_text" }),

                // унікальний: один відгук користувача на книгу
                new CreateIndexModel<Domain.Entities.Review>(
                    keys.Ascending("bookId").Ascending("author.userId"),
                    new CreateIndexOptions { Name = "ux_bookId_authorUserId", Unique = true })
            }, ct);

            await _context.Discussions.Indexes.CreateOneAsync(
                new CreateIndexModel<Domain.Entities.Discussion>(
                    Builders<Domain.Entities.Discussion>.IndexKeys.Ascending("bookId"),
                    new CreateIndexOptions { Name = "ix_bookId" }), cancellationToken: ct);

            _logger.LogInformation("MongoDB indexes ensured.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not create MongoDB indexes (is the database reachable?).");
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}

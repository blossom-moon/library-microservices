using Library.Reviews.Domain.Entities;
using Library.Reviews.Domain.ValueObjects;
using Library.Reviews.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Library.Reviews.Infrastructure.Seeding;

public class DiscussionSeeder : IDataSeeder
{
    private readonly MongoDbContext _context;
    private readonly ILogger<DiscussionSeeder> _logger;

    public DiscussionSeeder(MongoDbContext context, ILogger<DiscussionSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct)
    {
        if (await _context.Discussions.Find(FilterDefinition<Discussion>.Empty).AnyAsync(ct))
        {
            _logger.LogInformation("Discussions already seeded, skipping.");
            return;
        }

        var starter = new Author("user-olena", "Олена К.");
        var d1 = new Discussion(1, "Яка поема у «Кобзарі» найсильніша?", starter);
        d1.PostMessage(new Author("user-andriy", "Андрій М."), "Для мене — «Катерина».");
        d1.PostMessage(starter, "А я голосую за «Причинну».");
        var d2 = new Discussion(2, "Символіка в «Тінях забутих предків»", new Author("user-maria", "Марія Л."));

        await _context.Discussions.InsertManyAsync(new[] { d1, d2 }, cancellationToken: ct);
        _logger.LogInformation("Seeded {Count} discussions.", 2);
    }
}

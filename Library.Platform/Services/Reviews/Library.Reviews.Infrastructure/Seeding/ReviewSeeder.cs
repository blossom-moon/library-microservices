using Library.Reviews.Domain.Entities;
using Library.Reviews.Domain.ValueObjects;
using Library.Reviews.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Library.Reviews.Infrastructure.Seeding;

public class ReviewSeeder : IDataSeeder
{
    private readonly MongoDbContext _context;
    private readonly ILogger<ReviewSeeder> _logger;

    public ReviewSeeder(MongoDbContext context, ILogger<ReviewSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct)
    {
        // ідемпотентність: якщо дані вже є — нічого не робимо
        if (await _context.Reviews.Find(FilterDefinition<Review>.Empty).AnyAsync(ct))
        {
            _logger.LogInformation("Reviews already seeded, skipping.");
            return;
        }

        var olena = new Author("user-olena", "Олена К.");
        var andriy = new Author("user-andriy", "Андрій М.");
        var maria = new Author("user-maria", "Марія Л.");

        var r1 = new Review(1, "Кобзар", olena, 5, "Класика, яку варто перечитувати.");
        r1.AddComment(andriy, "Повністю погоджуюсь!");
        var r2 = new Review(1, "Кобзар", andriy, 4, "Сильна поезія, місцями складна мова.");
        var r3 = new Review(2, "Тіні забутих предків", maria, 5, "Атмосферна повість з Карпат.");
        r3.AddComment(olena, "Чудова екранізація теж.");
        var r4 = new Review(3, "Лісова пісня", olena, 4, "Красива драма-феєрія.");
        var r5 = new Review(3, "Лісова пісня", maria, 3, "Гарно, але затягнуто.");

        await _context.Reviews.InsertManyAsync(new[] { r1, r2, r3, r4, r5 }, cancellationToken: ct);
        _logger.LogInformation("Seeded {Count} reviews.", 5);
    }
}

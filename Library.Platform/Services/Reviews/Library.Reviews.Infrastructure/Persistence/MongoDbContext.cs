using Library.Reviews.Domain.Entities;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MongoDB.Driver.Core.Extensions.DiagnosticSources;

namespace Library.Reviews.Infrastructure.Persistence;

/// <summary>Єдина точка доступу до MongoDB. Налаштування приходять через IOptions.</summary>
public class MongoDbContext
{
    public IMongoClient Client { get; }
    public IMongoDatabase Database { get; }
    public IMongoCollection<Review> Reviews { get; }
    public IMongoCollection<Discussion> Discussions { get; }

    /// <summary>Оброблені події (_id = EventId як рядок): унікальність _id гарантує, що подію застосовано не двічі.</summary>
    public IMongoCollection<MongoDB.Bson.BsonDocument> ProcessedEvents { get; }

    public string ReviewsCollectionName { get; }

    public MongoDbContext(IOptions<MongoDbOptions> options)
    {
        MongoMappings.Register();   // BsonClassMap реєструються один раз при старті

        var o = options.Value;
        // Лаб. 05: підписка драйвера на трасування — кожна команда MongoDB стає span в OpenTelemetry
        var settings = MongoClientSettings.FromConnectionString(o.ConnectionString);
        settings.ClusterConfigurator = cb => cb.Subscribe(new DiagnosticsActivityEventSubscriber());
        Client = new MongoClient(settings);
        Database = Client.GetDatabase(o.DatabaseName);

        ReviewsCollectionName = o.ReviewsCollection;
        Reviews = Database.GetCollection<Review>(o.ReviewsCollection);
        Discussions = Database.GetCollection<Discussion>(o.DiscussionsCollection);
        ProcessedEvents = Database.GetCollection<MongoDB.Bson.BsonDocument>(o.ProcessedEventsCollection);
    }
}

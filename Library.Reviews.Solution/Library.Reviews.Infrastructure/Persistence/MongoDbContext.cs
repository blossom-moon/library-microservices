using Library.Reviews.Domain.Entities;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Library.Reviews.Infrastructure.Persistence;

/// <summary>Єдина точка доступу до MongoDB. Налаштування приходять через IOptions.</summary>
public class MongoDbContext
{
    public IMongoDatabase Database { get; }
    public IMongoCollection<Review> Reviews { get; }
    public IMongoCollection<Discussion> Discussions { get; }

    public string ReviewsCollectionName { get; }

    public MongoDbContext(IOptions<MongoDbOptions> options)
    {
        MongoMappings.Register();   // BsonClassMap реєструються один раз при старті

        var o = options.Value;
        var client = new MongoClient(o.ConnectionString);
        Database = client.GetDatabase(o.DatabaseName);

        ReviewsCollectionName = o.ReviewsCollection;
        Reviews = Database.GetCollection<Review>(o.ReviewsCollection);
        Discussions = Database.GetCollection<Discussion>(o.DiscussionsCollection);
    }
}

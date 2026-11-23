namespace Library.Reviews.Infrastructure.Persistence;

public class MongoDbOptions
{
    public const string SectionName = "MongoDb";

    public string ConnectionString { get; set; } = "";
    public string DatabaseName { get; set; } = "Library_Reviews_Lab04";
    public string ReviewsCollection { get; set; } = "reviews";
    public string DiscussionsCollection { get; set; } = "discussions";
}

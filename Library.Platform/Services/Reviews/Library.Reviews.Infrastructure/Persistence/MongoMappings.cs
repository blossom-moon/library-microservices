using Library.Reviews.Domain.Common;
using Library.Reviews.Domain.Entities;
using Library.Reviews.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;

namespace Library.Reviews.Infrastructure.Persistence;

/// <summary>
/// Відображення доменних типів на BSON через BsonClassMap — без жодних атрибутів у Domain.
/// Викликається один раз (захист від повторної реєстрації).
/// </summary>
public static class MongoMappings
{
    private static readonly object Lock = new();
    private static bool _registered;

    public static void Register()
    {
        lock (Lock)
        {
            if (_registered) return;

            // Конвенції мають бути зареєстровані до створення class map
            ConventionRegistry.Register("LibraryReviewsConventions", new ConventionPack
            {
                new CamelCaseElementNameConvention(),
                new IgnoreExtraElementsConvention(true)
            }, t => t.Namespace != null && t.Namespace.StartsWith("Library.Reviews.Domain"));

            // --- Value Objects: незмінні, відновлюються через конструктор ---
            BsonClassMap.RegisterClassMap<Author>(cm =>
            {
                cm.MapProperty(x => x.UserId).SetElementName("userId");
                cm.MapProperty(x => x.DisplayName).SetElementName("displayName");
                cm.MapConstructor(
                    typeof(Author).GetConstructor(new[] { typeof(string), typeof(string) })!,
                    "UserId", "DisplayName");
            });

            BsonClassMap.RegisterClassMap<Comment>(cm =>
            {
                cm.MapProperty(x => x.Author).SetElementName("author");
                cm.MapProperty(x => x.Text).SetElementName("text");
                cm.MapProperty(x => x.CreatedAt).SetElementName("createdAt").SetSerializer(Utc);
                cm.MapConstructor(
                    typeof(Comment).GetConstructor(new[] { typeof(Author), typeof(string), typeof(DateTime) })!,
                    "Author", "Text", "CreatedAt");
            });

            // --- Сутності: спільні поля BaseEntity мапляться один раз ---
            BsonClassMap.RegisterClassMap<BaseEntity>(cm =>
            {
                cm.MapIdProperty(x => x.Id);
                cm.MapProperty(x => x.CreatedAt).SetElementName("createdAt").SetSerializer(Utc);
                cm.MapProperty(x => x.UpdatedAt).SetElementName("updatedAt")
                    .SetSerializer(new NullableSerializer<DateTime>(Utc));
                cm.MapProperty(x => x.Version).SetElementName("version");
            });

            BsonClassMap.RegisterClassMap<Review>(cm =>
            {
                cm.MapProperty(x => x.BookId).SetElementName("bookId");
                cm.MapProperty(x => x.BookTitle).SetElementName("bookTitle");
                cm.MapProperty(x => x.BookTitleSyncedAt).SetElementName("bookTitleSyncedAt")
                    .SetSerializer(new NullableSerializer<DateTime>(Utc));
                cm.MapProperty(x => x.Author).SetElementName("author");
                cm.MapProperty(x => x.Rating).SetElementName("rating");
                cm.MapProperty(x => x.Text).SetElementName("text");
                cm.MapField("_comments").SetElementName("comments");   // приватне поле → вкладений масив
            });

            BsonClassMap.RegisterClassMap<Discussion>(cm =>
            {
                cm.MapProperty(x => x.BookId).SetElementName("bookId");
                cm.MapProperty(x => x.Title).SetElementName("title");
                cm.MapProperty(x => x.StartedBy).SetElementName("startedBy");
                cm.MapField("_messages").SetElementName("messages");
            });

            _registered = true;
        }
    }

    private static DateTimeSerializer Utc => new(DateTimeKind.Utc);
}

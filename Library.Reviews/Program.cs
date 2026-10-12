using MongoDB.Bson;
using MongoDB.Driver;
using System.Linq;
var client = new MongoClient("mongodb+srv://kostetskakhrystyna_db_user:8uBA4GGFlcGoBW0B@blossom.2hsrtgt.mongodb.net/?appName=blossom");
var db = client.GetDatabase("Library_Reviews");

var reviews = db.GetCollection<BsonDocument>("reviews");

var review = new BsonDocument
{
    { "bookId", 101 },
    { "readerId", 55 },
    { "readerName", "Олена Коваль" },
    { "rating", 5 },
    { "text", "Чудова книга, рекомендую!" },
    { "createdAt", DateTime.UtcNow },
    { "comments", new BsonArray
        {
            new BsonDocument { { "readerId", 12 }, { "text", "Погоджуюсь!" }, { "createdAt", DateTime.UtcNow } }
        }
    },
    { "tags", new BsonArray { "fantasy", "must-read" } }
};

reviews.InsertOne(review);
Console.WriteLine("Review inserted with _id = " + review["_id"]);

var found = reviews.Find(new BsonDocument("bookId", 101)).ToList();
Console.WriteLine($"Found {found.Count} review(s) for bookId=101:");
foreach (var r in found)
    Console.WriteLine(r.ToJson());
var messages = db.GetCollection<BsonDocument>("messages");
var discussions = db.GetCollection<BsonDocument>("discussions");

var msg1 = new BsonDocument { { "text", "Хто ще читав цю книгу?" }, { "authorId", 55 }, { "createdAt", DateTime.UtcNow } };
var msg2 = new BsonDocument { { "text", "Я читав, дуже сподобалось!" }, { "authorId", 12 }, { "createdAt", DateTime.UtcNow } };
messages.InsertMany(new[] { msg1, msg2 });

var discussion = new BsonDocument
{
    { "bookId", 101 },
    { "title", "Чи варто читати продовження?" },
    { "startedBy", 55 },
    { "createdAt", DateTime.UtcNow },
    { "messageIds", new BsonArray { msg1["_id"], msg2["_id"] } },   // referencing
    { "participantsCount", 2 }
};
discussions.InsertOne(discussion);

Console.WriteLine("Discussion inserted with _id = " + discussion["_id"]);

// Приклад "join" через referencing — довантажуємо повідомлення за їх Id
var ids = discussion["messageIds"].AsBsonArray.Select(x => x.AsObjectId).ToList();
var loadedMessages = messages.Find(Builders<BsonDocument>.Filter.In("_id", ids)).ToList();
Console.WriteLine("Messages in this discussion:");
foreach (var m in loadedMessages)
    Console.WriteLine(" - " + m["text"]);
var ratings = db.GetCollection<BsonDocument>("ratings");

// Aggregate pipeline: рахуємо середній рейтинг і кількість відгуків по bookId=101
var pipeline = new[]
{
    new BsonDocument("$match", new BsonDocument("bookId", 101)),
    new BsonDocument("$group", new BsonDocument
    {
        { "_id", "$bookId" },
        { "avgRating", new BsonDocument("$avg", "$rating") },
        { "totalReviews", new BsonDocument("$sum", 1) }
    })
};

var agg = reviews.Aggregate<BsonDocument>(pipeline).First();
Console.WriteLine("Aggregated: " + agg.ToJson());

var ratingDoc = new BsonDocument
{
    { "_id", "book_101" },
    { "bookId", 101 },
    { "bookTitle", "Захар Беркут" },
    { "avgRating", agg["avgRating"] },
    { "totalReviews", agg["totalReviews"] },
    { "updatedAt", DateTime.UtcNow }
};

ratings.ReplaceOne(
    Builders<BsonDocument>.Filter.Eq("_id", "book_101"),
    ratingDoc,
    new ReplaceOptions { IsUpsert = true });

Console.WriteLine("Ratings document upserted.");
reviews.Indexes.CreateOne(new CreateIndexModel<BsonDocument>(
    Builders<BsonDocument>.IndexKeys.Ascending("bookId").Descending("createdAt")));

discussions.Indexes.CreateOne(new CreateIndexModel<BsonDocument>(
    Builders<BsonDocument>.IndexKeys.Ascending("bookId")));

ratings.Indexes.CreateOne(new CreateIndexModel<BsonDocument>(
    Builders<BsonDocument>.IndexKeys.Ascending("bookId"),
    new CreateIndexOptions { Unique = true }));

Console.WriteLine("Indexes created.");
var shortReview = new BsonDocument
{
    { "bookId", 102 },
    { "readerId", 77 },
    { "readerName", "Максим Гуцул" },
    { "rating", 3 },
    { "text", "Непогано, але очікував більшого." },
    { "createdAt", DateTime.UtcNow }
    // немає ні "comments", ні "tags" — на відміну від review з bookId=101
};

reviews.InsertOne(shortReview);
Console.WriteLine("Short review inserted (no comments/tags field).");

// Ще один приклад — review з додатковим полем, якого немає в інших документах
var reviewWithPhoto = new BsonDocument
{
    { "bookId", 103 },
    { "readerId", 90 },
    { "readerName", "Ірина Бойко" },
    { "rating", 4 },
    { "text", "Гарне видання, ілюстрації чудові." },
    { "createdAt", DateTime.UtcNow },
    { "photoUrl", "https://example.com/photos/book103.jpg" },   // поля, якого немає в інших документах
    { "verifiedPurchase", true }
};

reviews.InsertOne(reviewWithPhoto);
Console.WriteLine("Review with photoUrl/verifiedPurchase inserted.");

// Перевірка: виводимо всі поля кожного документа, щоб показати різну структуру
Console.WriteLine("\n--- Схема документів у reviews (демонстрація гнучкості) ---");
foreach (var doc in reviews.Find(new BsonDocument()).ToList())
{
    var fields = string.Join(", ", doc.Names);
    Console.WriteLine($"bookId={doc["bookId"]}: [{fields}]");
}
Console.WriteLine("\n--- Варіанти запитів ---");

// 1) Фільтр за рейтингом (>= 4) + сортування за датою (найновіші першими)
var highRated = reviews.Find(Builders<BsonDocument>.Filter.Gte("rating", 4))
                        .Sort(Builders<BsonDocument>.Sort.Descending("createdAt"))
                        .ToList();
Console.WriteLine($"1) Відгуки з рейтингом >= 4 ({highRated.Count} шт.):");
foreach (var r in highRated)
    Console.WriteLine($"   bookId={r["bookId"]}, rating={r["rating"]}");

// 2) Проєкція — тільки потрібні поля, без зайвого (readerName, text)
var projection = Builders<BsonDocument>.Projection.Include("bookId").Include("readerName").Include("rating").Exclude("_id");
var titlesOnly = reviews.Find(new BsonDocument()).Project(projection).ToList();
Console.WriteLine($"\n2) Проєкція (тільки bookId, readerName, rating):");
foreach (var r in titlesOnly)
    Console.WriteLine("   " + r.ToJson());

// 3) Пагінація — 2 відгуки на "сторінку", друга сторінка
var page2 = reviews.Find(new BsonDocument())
                    .Sort(Builders<BsonDocument>.Sort.Ascending("createdAt"))
                    .Skip(2).Limit(2)
                    .ToList();
Console.WriteLine($"\n3) Пагінація (сторінка 2, по 2 записи):");
foreach (var r in page2)
    Console.WriteLine($"   bookId={r["bookId"]}, createdAt={r["createdAt"]}");

// 4) Текстовий пошук по полю text (регулярний вираз, часткове співпадіння)
var textSearch = reviews.Find(Builders<BsonDocument>.Filter.Regex("text", new BsonRegularExpression("чудов", "i"))).ToList();
Console.WriteLine($"\n4) Пошук за словом 'чудов' у тексті ({textSearch.Count} шт.):");
foreach (var r in textSearch)
    Console.WriteLine($"   bookId={r["bookId"]}: {r["text"]}");
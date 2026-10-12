// Library_Reviews — ідемпотентні тестові дані (Проєкт №3).
// Виконати ПІСЛЯ collections.js. Можна запускати повторно — усе йде через
// updateOne(..., { upsert: true }) за природним ключем, дублікатів не буде.
//
// bookId узгоджений з db/p1/db/p2 (ті самі BookId, що в Library_Loans і
// Library_Catalog) — так підтримується дублювання ідентифікатора книги між
// трьома сервісами без FOREIGN KEY.

use("Library_Reviews");

db.reviews.updateOne(
    { bookId: NumberInt(1), readerId: NumberInt(1) },
    { $setOnInsert: {
        bookId: NumberInt(1), readerId: NumberInt(1), readerName: "Христина Костецька",
        rating: 5, text: "Один із найважливіших творів української літератури.",
        createdAt: new Date()
    }},
    { upsert: true }
);

db.reviews.updateOne(
    { bookId: NumberInt(2), readerId: NumberInt(2) },
    { $setOnInsert: {
        bookId: NumberInt(2), readerId: NumberInt(2), readerName: "Іван Петренко",
        rating: 4, text: "Сильна, але важка книга.",
        createdAt: new Date(),
        tags: ["воєнна проза", "рекомендую"]
    }},
    { upsert: true }
);

db.reviews.updateOne(
    { bookId: NumberInt(3), readerId: NumberInt(1) },
    { $setOnInsert: {
        bookId: NumberInt(3), readerId: NumberInt(1), readerName: "Христина Костецька",
        rating: 5, text: "Класика модернізму, рекомендую.",
        createdAt: new Date(),
        comments: [
            { authorName: "Олена Коваль", text: "Погоджуюсь, чудовий стиль.", createdAt: new Date() }
        ]
    }},
    { upsert: true }
);

// messages — створюються першими, бо discussions посилається на їхні _id
const msg1 = db.messages.findOneAndUpdate(
    { text: "Хто вже прочитав 'Кобзар' цього місяця?" },
    { $setOnInsert: { text: "Хто вже прочитав 'Кобзар' цього місяця?", authorId: NumberInt(1), createdAt: new Date() } },
    { upsert: true, returnDocument: "after" }
);
const msg2 = db.messages.findOneAndUpdate(
    { text: "Я читав, дуже сподобалось!" },
    { $setOnInsert: { text: "Я читав, дуже сподобалось!", authorId: NumberInt(2), createdAt: new Date() } },
    { upsert: true, returnDocument: "after" }
);

db.discussions.updateOne(
    { bookId: NumberInt(1), title: "Обговорення: Кобзар" },
    { $setOnInsert: {
        bookId: NumberInt(1),
        title: "Обговорення: Кобзар",
        startedBy: NumberInt(1),
        createdAt: new Date(),
        messageIds: [msg1._id, msg2._id],
        participantsCount: NumberInt(2)
    }},
    { upsert: true }
);

db.ratings.updateOne(
    { bookId: NumberInt(1) },
    { $set: { bookId: NumberInt(1), bookTitle: "Кобзар", avgRating: 5.0, totalReviews: 1, updatedAt: new Date() } },
    { upsert: true }
);

db.ratings.updateOne(
    { bookId: NumberInt(2) },
    { $set: { bookId: NumberInt(2), bookTitle: "Тигролови", avgRating: 4.0, totalReviews: 1, updatedAt: new Date() } },
    { upsert: true }
);

print("Seed-дані готові.");

// Library_Reviews — MongoDB (Проєкт №3).
// Колекції вже існують у кластері; цей скрипт лише створює індекси
// (ідемпотентно — createIndex можна запускати повторно).
//
// Три підходи до моделювання даних, представлені в цих колекціях:
//   - REFERENCING: discussions.messageIds — масив посилань (ObjectId) на
//     окремі документи в messages, без дублювання їхнього вмісту.
//   - EMBEDDING: у reviews деякі документи мають вкладений масив comments —
//     коментарі живуть прямо всередині документа відгуку.
//   - ГІБРИДНИЙ: reviews зберігає readerId (посилання на Library.Loans) і
//     водночас дублює readerName для швидкого відображення без join;
//     ratings — агрегований знімок на книгу (bookId-посилання + продубльована
//     bookTitle з Library.Catalog), що оновлюється після кожного нового відгуку.
//
// Виконати в mongosh, підключеному до кластера:
//   mongosh "<connection string з Atlas>"
//   load("collections.js")

use("Library_Reviews");

db.reviews.createIndex({ bookId: 1 });
db.reviews.createIndex({ readerId: 1 });

db.discussions.createIndex({ bookId: 1 });
db.messages.createIndex({ authorId: 1 });
db.ratings.createIndex({ bookId: 1 }, { unique: true });

print("Індекси готові.");

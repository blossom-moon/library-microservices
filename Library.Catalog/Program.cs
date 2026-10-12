using Microsoft.EntityFrameworkCore;
using Library.Catalog;
using Library.Catalog;

using var db = new LibraryCatalogContext();

var author = new Author { FullName = "Іван Франко" };
// Прибираємо тестові дані з попереднього запуску, якщо є
var existing = db.Books.Include(b => b.Detail).Include(b => b.BookAuthors)
                        .FirstOrDefault(b => b.Isbn == "978-966-03-9999-1");
if (existing != null)
{
    db.BookAuthors.RemoveRange(existing.BookAuthors);
    if (existing.Detail != null) db.BookDetails.Remove(existing.Detail);
    db.Books.Remove(existing);
    db.SaveChanges();
}
var book = new Book
{
    Isbn = "978-966-03-9999-1",
    Title = "Захар Беркут",
    Price = 250.00m,
    PublisherId = 1,
    CategoryId = 1,
    Detail = new BookDetail
    {
        Annotation = "Історична повість про боротьбу карпатського села.",
        PageCount = 180,
        Language = "uk"
    },
    BookAuthors = new List<BookAuthor>
    {
        new BookAuthor { Author = author, Role = "Author" }
    }
};

db.Books.Add(book);
db.SaveChanges();

Console.WriteLine($"Book saved with Id = {book.BookId}");

var loaded = db.Books
    .Include(b => b.Detail)
    .Include(b => b.BookAuthors).ThenInclude(ba => ba.Author)
    .First(b => b.BookId == book.BookId);

Console.WriteLine($"{loaded.Title} — {loaded.Detail.PageCount} pages, author: {loaded.BookAuthors.First().Author.FullName}");
// UPDATE — змінюємо ціну книги
var bookToUpdate = db.Books.First(b => b.BookId == book.BookId);
bookToUpdate.Price = 275.50m;
db.SaveChanges();
Console.WriteLine($"Updated price: {bookToUpdate.Price}");

// DELETE — видаляємо книгу (каскадно видалить і BookDetail, і BookAuthor через FK-конфігурацію)
var bookToDelete = db.Books.Include(b => b.Detail).Include(b => b.BookAuthors)
                            .First(b => b.BookId == book.BookId);
db.BookAuthors.RemoveRange(bookToDelete.BookAuthors);
db.BookDetails.Remove(bookToDelete.Detail);
db.Books.Remove(bookToDelete);
db.SaveChanges();
Console.WriteLine("Book and related records deleted.");

Console.WriteLine($"Books remaining: {db.Books.Count()}");
// Створюємо тестову книгу для демонстрації soft delete
var testBook = new Book
{
    Isbn = "978-000-00-0000-1",
    Title = "Тестова книга для soft delete",
    Price = 100.00m,
    PublisherId = 1,
    CategoryId = 1,
    Detail = new BookDetail { Annotation = "Тест", PageCount = 50, Language = "uk" }
};
db.Books.Add(testBook);
db.SaveChanges();
Console.WriteLine($"Test book created, Id = {testBook.BookId}, CreatedAt = {testBook.CreatedAt}");

// Soft delete замість фізичного видалення
testBook.IsDeleted = true;
testBook.UpdatedAt = DateTime.UtcNow;
testBook.UpdatedBy = "admin";
db.SaveChanges();
Console.WriteLine("Test book soft-deleted.");

// Перевірка: звичайний запит НЕ бачить soft-deleted книгу (працює query filter)
var visibleCount = db.Books.Count(b => b.Isbn == "978-000-00-0000-1");
Console.WriteLine($"Visible via normal query (should be 0): {visibleCount}");

// Перевірка: IgnoreQueryFilters бачить її (для адмін-панелі/аудиту)
var stillExists = db.Books.IgnoreQueryFilters().Count(b => b.Isbn == "978-000-00-0000-1");
Console.WriteLine($"Still exists in DB via IgnoreQueryFilters (should be 1): {stillExists}");
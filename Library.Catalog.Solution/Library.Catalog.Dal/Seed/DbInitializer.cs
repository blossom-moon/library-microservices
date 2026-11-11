using Library.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Library.Catalog.Dal.Seed;

/// <summary>
/// Програмний ідемпотентний сідінг. Довідники (Publishers, Categories) уже створені
/// міграцією через HasData із фіксованими Id, тому тут спочатку автори, потім залежні книги.
/// </summary>
public static class DbInitializer
{
    public static async Task SeedAsync(CatalogDbContext db, CancellationToken ct = default)
    {
        if (await db.Books.AnyAsync(ct)) return;   // уже наповнено — повторний запуск нічого не дублює

        var franko = new Author("Іван Франко");
        var lesya = new Author("Леся Українка");
        var shevchenko = new Author("Тарас Шевченко");
        var kotsiubynsky = new Author("Михайло Коцюбинський");
        var kostenko = new Author("Ліна Костенко");
        var kobylianska = new Author("Ольга Кобилянська");
        var pidmohylny = new Author("Валер'ян Підмогильний");
        var lem = new Author("Станіслав Лем");
        db.Authors.AddRange(franko, lesya, shevchenko, kotsiubynsky, kostenko, kobylianska, pidmohylny, lem);

        var now = DateTime.UtcNow;
        var books = new List<Book>
        {
            Create("978-966-03-0001-1", "Захар Беркут", 250.00m, 1, 2, 12, "Історична повість про боротьбу карпатської громади.", 180, franko, now.AddDays(-400)),
            Create("978-966-03-0002-8", "Лісова пісня", 180.00m, 2, 3, 8, "Драма-феєрія за мотивами волинського фольклору.", 144, lesya, now.AddDays(-300)),
            Create("978-966-03-0003-5", "Кобзар", 320.00m, 3, 3, 20, "Збірка поезій Тараса Шевченка.", 560, shevchenko, now.AddDays(-250)),
            Create("978-966-03-0004-2", "Тіні забутих предків", 210.00m, 1, 4, 6, "Повість про життя гуцулів у Карпатах.", 128, kotsiubynsky, now.AddDays(-200)),
            Create("978-966-03-0005-9", "Intermezzo", 160.00m, 2, 4, 5, "Імпресіоністична новела.", 64, kotsiubynsky, now.AddDays(-150)),
            Create("978-966-03-0006-6", "Маруся Чурай", 275.50m, 3, 3, 9, "Історичний роман у віршах.", 192, kostenko, now.AddDays(-120)),
            Create("978-966-03-0007-3", "Землі", 195.00m, 1, 4, 4, "Повість про зв'язок людини із землею.", 176, kobylianska, now.AddDays(-90)),
            Create("978-966-03-0008-0", "Fata Morgana", 230.00m, 2, 4, 7, "Повість про революційні події на селі.", 152, kotsiubynsky, now.AddDays(-60)),
            Create("978-966-03-0009-7", "Каменярі", 140.00m, 3, 3, 10, "Збірка віршів Івана Франка.", 96, franko, now.AddDays(-45)),
            Create("978-966-03-0010-3", "Contra spem spero", 120.00m, 1, 3, 3, "Добірка поезій Лесі Українки.", 88, lesya, now.AddDays(-30)),
            Create("978-966-03-0011-0", "Записки українського самашедшого", 260.00m, 2, 4, 11, "Роман-сповідь у формі щоденника.", 304, kostenko, now.AddDays(-20)),
            Create("978-966-03-0012-7", "Місто", 300.00m, 3, 2, 6, "Роман про становлення молодого інтелектуала.", 288, pidmohylny, now.AddDays(-10)),
            Create("978-966-03-0013-4", "Соляріс", 340.00m, 1, 1, 14, "Науково-фантастичний роман.", 256, lem, now.AddDays(-5)),
        };

        db.Books.AddRange(books);
        await db.SaveChangesAsync(ct);   // один SaveChanges: автори → книги → деталі → зв'язки
    }

    private static Book Create(string isbn, string title, decimal price, long publisherId, long categoryId,
        int copies, string annotation, int pages, Author author, DateTime addedAt)
    {
        var book = new Book(isbn, title, price, publisherId, categoryId, copies);
        book.SetDetail(annotation, pages, "uk");
        book.AddAuthor(author, "Author", addedAt);
        return book;
    }
}

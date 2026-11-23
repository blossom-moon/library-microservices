using Library.Catalog.Domain.Exceptions;

namespace Library.Catalog.Domain.Entities;

/// <summary>
/// Книга каталогу. Бізнес-правила — у методах сутності, а не у відкритих setter-ах
/// (аналог CatalogItem з eShop): ціна перевіряється в конструкторі та ChangePrice,
/// залишок змінюється лише RemoveCopies / AddCopies.
/// </summary>
public class Book
{
    public long BookId { get; private set; }
    public string Isbn { get; private set; } = "";
    public string Title { get; private set; } = "";
    public decimal Price { get; private set; }
    public int AvailableCopies { get; private set; }
    public DateTime CreatedAt { get; private set; }      // DEFAULT SYSUTCDATETIME() у БД

    public long PublisherId { get; private set; }
    public Publisher Publisher { get; private set; } = null!;   // N:1

    public long CategoryId { get; private set; }
    public Category Category { get; private set; } = null!;     // N:1

    public BookDetail? Detail { get; private set; }             // 1:1
    public List<BookAuthor> BookAuthors { get; } = new();       // M:N

    private Book() { }   // для EF Core

    public Book(string isbn, string title, decimal price, long publisherId, long categoryId, int availableCopies = 0)
    {
        if (string.IsNullOrWhiteSpace(isbn)) throw new DomainException("ISBN is required.");
        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("Title is required.");
        if (price <= 0) throw new DomainException("Price must be positive.");
        if (availableCopies < 0) throw new DomainException("Available copies cannot be negative.");

        Isbn = isbn;
        Title = title;
        Price = price;
        PublisherId = publisherId;
        CategoryId = categoryId;
        AvailableCopies = availableCopies;
    }

    public void ChangePrice(decimal newPrice)
    {
        if (newPrice <= 0) throw new DomainException("Price must be positive.");
        Price = newPrice;
    }

    public void RemoveCopies(int quantity)
    {
        if (quantity <= 0) throw new DomainException("Quantity must be positive.");
        if (AvailableCopies < quantity)
            throw new DomainException($"Only {AvailableCopies} copies available.");
        AvailableCopies -= quantity;
    }

    public void AddCopies(int quantity)
    {
        if (quantity <= 0) throw new DomainException("Quantity must be positive.");
        AvailableCopies += quantity;
    }

    public void SetDetail(string annotation, int pageCount, string language)
        => Detail = new BookDetail(annotation, pageCount, language);

    public void AddAuthor(Author author, string role = "Author", DateTime? addedAt = null)
    {
        if (BookAuthors.Any(ba => ReferenceEquals(ba.Author, author)))
            throw new DomainException($"Author '{author.FullName}' is already added to this book.");
        BookAuthors.Add(new BookAuthor(author, role, addedAt ?? DateTime.UtcNow));
    }
}

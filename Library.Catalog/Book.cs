namespace Library.Catalog;

public class Book
{
    public long BookId { get; set; }
    public string Isbn { get; set; } = null!;
    public string Title { get; set; } = null!;
    public decimal Price { get; set; }

    public long PublisherId { get; set; }
    public Publisher Publisher { get; set; } = null!;

    public long CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public BookDetail Detail { get; set; } = null!;
    public ICollection<BookAuthor> BookAuthors { get; set; } = new List<BookAuthor>();

    // Аудит
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = "system";
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    // Оптимістична конкурентність
    public byte[] RowVersion { get; set; } = null!;
}
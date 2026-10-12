namespace Library.Catalog;

public class Publisher
{
    public long PublisherId { get; set; }
    public string Name { get; set; } = null!;
    public string Country { get; set; } = null!;
    public ICollection<Book> Books { get; set; } = new List<Book>();
}
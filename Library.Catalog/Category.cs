namespace Library.Catalog;

public class Category
{
    public long CategoryId { get; set; }
    public string Name { get; set; } = null!;
    public ICollection<Book> Books { get; set; } = new List<Book>();
}
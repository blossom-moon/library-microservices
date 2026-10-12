namespace Library.Catalog;

public class BookDetail
{
    public long BookId { get; set; }
    public Book Book { get; set; } = null!;
    public string Annotation { get; set; } = null!;
    public int PageCount { get; set; }
    public string Language { get; set; } = null!;
}
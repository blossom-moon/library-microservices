namespace Library.Catalog;

public class Author
{
    public long AuthorId { get; set; }
    public string FullName { get; set; } = null!;
    public ICollection<BookAuthor> BookAuthors { get; set; } = new List<BookAuthor>();
}
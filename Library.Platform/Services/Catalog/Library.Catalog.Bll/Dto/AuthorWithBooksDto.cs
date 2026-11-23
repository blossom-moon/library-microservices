namespace Library.Catalog.Bll.Dto;

public sealed class AuthorBookDto
{
    public long BookId { get; set; }
    public string Isbn { get; set; } = "";
    public string Title { get; set; } = "";
    public string Role { get; set; } = "";
    public DateTime AddedAt { get; set; }
}

/// <summary>Автор із книгами (результат Explicit Loading).</summary>
public sealed class AuthorWithBooksDto
{
    public long AuthorId { get; set; }
    public string FullName { get; set; } = "";
    public List<AuthorBookDto> Books { get; set; } = new();
}

namespace Library.Catalog.Bll.Dto;

public sealed class BookAuthorInputDto
{
    public long AuthorId { get; set; }
    public string Role { get; set; } = "Author";
}

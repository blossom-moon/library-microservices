namespace Library.Catalog.Bll.Dto;

public sealed class BookAuthorDto
{
    public long AuthorId { get; set; }
    public string FullName { get; set; } = "";
    public string Role { get; set; } = "";
    public DateTime AddedAt { get; set; }
}

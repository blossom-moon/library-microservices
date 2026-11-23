namespace Library.Catalog.Bll.Dto;

public sealed class BookShortDto
{
    public long BookId { get; set; }
    public string Isbn { get; set; } = "";
    public string Title { get; set; } = "";
    public decimal Price { get; set; }
}

namespace Library.Catalog.Bll.Dto;

public class BookDto
{
    public long BookId { get; set; }
    public string Isbn { get; set; } = "";
    public string Title { get; set; } = "";
    public decimal Price { get; set; }
    public int AvailableCopies { get; set; }
    public string PublisherName { get; set; } = "";
    public string CategoryName { get; set; } = "";
}

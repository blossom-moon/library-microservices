namespace Library.Loans.Bll.Dto;

public class BookDto
{
    public long BookId { get; set; }
    public string Isbn { get; set; } = null!;
    public string Title { get; set; } = null!;
    public bool IsAvailable { get; set; }
}
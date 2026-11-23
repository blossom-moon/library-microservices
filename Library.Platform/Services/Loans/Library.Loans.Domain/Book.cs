namespace Library.Loans.Domain;

public class Book
{
    public long BookId { get; set; }
    public string Isbn { get; set; } = null!;
    public string Title { get; set; } = null!;
    public bool IsAvailable { get; set; }
}
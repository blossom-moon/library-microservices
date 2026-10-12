namespace Library.Loans.Domain;

public class LoanItem
{
    public long LoanItemId { get; set; }
    public long LoanId { get; set; }
    public long BookId { get; set; }
    public string BookTitle { get; set; } = null!;
    public DateTime? ReturnedAt { get; set; }
}
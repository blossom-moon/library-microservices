namespace Library.Loans.Domain;

public class Loan
{
    public long LoanId { get; set; }
    public long ReaderId { get; set; }
    public string Status { get; set; } = null!;
    public DateTime LoanDate { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? ReturnDate { get; set; }
    public decimal FineAmount { get; set; }
    public bool IsDeleted { get; set; }

    // Заповнюється через multi-mapping JOIN у LoanRepository.GetWithItemsAsync;
    // для простих вибірок (GetByIdAsync без деталей) лишається порожнім.
    public List<LoanItem> Items { get; set; } = new();
}

namespace Library.Loans.Domain;

public class Book
{
    public long BookId { get; set; }
    public string Isbn { get; set; } = null!;
    public string Title { get; set; } = null!;
    public bool IsAvailable { get; set; }

    // Лаб. 08: поля локальної копії, що оновлюються подіями Каталогу (джерело істини — Catalog)
    public decimal? Price { get; set; }
    public DateTime? PriceChangedAt { get; set; }    // OccurredAt останньої застосованої BookPriceChangedEvent
    public DateTime? TitleChangedAt { get; set; }    // OccurredAt останньої застосованої BookRenamedEvent
}
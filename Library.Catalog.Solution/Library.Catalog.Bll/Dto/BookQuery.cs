namespace Library.Catalog.Bll.Dto;

/// <summary>Параметри пагінації, фільтрації та сортування списку книг.</summary>
public sealed class BookQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public long? CategoryId { get; set; }
    public long? PublisherId { get; set; }

    /// <summary>title | price | isbn | copies (за замовчуванням title).</summary>
    public string? SortBy { get; set; }

    /// <summary>asc | desc (за замовчуванням asc).</summary>
    public string? SortDir { get; set; }
}

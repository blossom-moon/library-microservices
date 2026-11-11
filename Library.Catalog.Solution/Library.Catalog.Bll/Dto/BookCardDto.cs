namespace Library.Catalog.Bll.Dto;

/// <summary>Картка книги (результат Eager Loading): деталі, видавець, категорія, автори.</summary>
public sealed class BookCardDto : BookDto
{
    public string Annotation { get; set; } = "";
    public int PageCount { get; set; }
    public string Language { get; set; } = "";
    public List<BookAuthorDto> Authors { get; set; } = new();
}

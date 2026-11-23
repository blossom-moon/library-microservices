using System.ComponentModel.DataAnnotations;

namespace Library.Catalog.Bll.Dto;

/// <summary>
/// Прості правила (обов'язковість, довжина) — DataAnnotations; складні (формат ISBN, ціна,
/// існування зв'язків) — FluentValidation у CreateBookDtoValidator. Без дублювання.
/// </summary>
public sealed class CreateBookDto
{
    [Required, StringLength(20)]
    public string Isbn { get; set; } = "";

    [Required, StringLength(300)]
    public string Title { get; set; } = "";

    public decimal Price { get; set; }
    public int AvailableCopies { get; set; }
    public long PublisherId { get; set; }
    public long CategoryId { get; set; }

    [Required, StringLength(2000)]
    public string Annotation { get; set; } = "";

    public int PageCount { get; set; }

    [Required, StringLength(10)]
    public string Language { get; set; } = "uk";

    public List<BookAuthorInputDto> Authors { get; set; } = new();
}

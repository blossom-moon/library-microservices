using Library.Catalog.Domain.Exceptions;

namespace Library.Catalog.Domain.Entities;

/// <summary>1:1 до Book — PK є водночас FK.</summary>
public class BookDetail
{
    public long BookId { get; private set; }
    public Book Book { get; private set; } = null!;
    public string Annotation { get; private set; } = "";
    public int PageCount { get; private set; }
    public string Language { get; private set; } = "uk";

    private BookDetail() { }   // для EF Core

    public BookDetail(string annotation, int pageCount, string language)
    {
        if (pageCount <= 0) throw new DomainException("Page count must be positive.");
        Annotation = annotation;
        PageCount = pageCount;
        Language = language;
    }
}

using Library.Catalog.Domain.Exceptions;

namespace Library.Catalog.Domain.Entities;

/// <summary>
/// Проміжна сутність M:N Book ↔ Author зі складеним ключем (BookId, AuthorId)
/// і власними полями Role та AddedAt.
/// </summary>
public class BookAuthor
{
    public long BookId { get; private set; }
    public Book Book { get; private set; } = null!;

    public long AuthorId { get; private set; }
    public Author Author { get; private set; } = null!;

    public string Role { get; private set; } = "Author";
    public DateTime AddedAt { get; private set; }

    private BookAuthor() { }   // для EF Core

    internal BookAuthor(Author author, string role, DateTime addedAt)
    {
        if (string.IsNullOrWhiteSpace(role)) throw new DomainException("Author role is required.");
        Author = author;
        Role = role;
        AddedAt = addedAt;
    }
}

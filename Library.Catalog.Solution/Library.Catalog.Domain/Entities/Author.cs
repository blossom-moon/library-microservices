using Library.Catalog.Domain.Exceptions;

namespace Library.Catalog.Domain.Entities;

public class Author
{
    public long AuthorId { get; private set; }
    public string FullName { get; private set; } = "";

    public List<BookAuthor> BookAuthors { get; } = new();   // M:N через BookAuthor

    private Author() { }   // для EF Core

    public Author(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) throw new DomainException("Author name is required.");
        FullName = fullName;
    }
}

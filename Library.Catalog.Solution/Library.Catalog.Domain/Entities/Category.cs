using Library.Catalog.Domain.Exceptions;

namespace Library.Catalog.Domain.Entities;

public class Category
{
    public long CategoryId { get; private set; }
    public string Name { get; private set; } = "";

    public List<Book> Books { get; } = new();

    private Category() { }   // для EF Core

    public Category(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Category name is required.");
        Name = name;
    }
}

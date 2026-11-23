using Library.Catalog.Domain.Exceptions;

namespace Library.Catalog.Domain.Entities;

public class Publisher
{
    public long PublisherId { get; private set; }
    public string Name { get; private set; } = "";
    public string Country { get; private set; } = "";

    public List<Book> Books { get; } = new();

    private Publisher() { }   // для EF Core

    public Publisher(string name, string country)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Publisher name is required.");
        Name = name;
        Country = country;
    }
}

using Library.Catalog.Domain.Entities;
using Library.Catalog.Domain.Exceptions;

namespace Library.Catalog.Tests;

public class BookTests
{
    private static Book NewBook(decimal price = 100m, int copies = 5)
        => new("978-966-03-0001-1", "Тест", price, 1, 1, copies);

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Constructor_NonPositivePrice_Throws(decimal price)
        => Assert.Throws<DomainException>(() => NewBook(price));

    [Fact]
    public void ChangePrice_Positive_UpdatesPrice()
    {
        var book = NewBook();
        book.ChangePrice(250.5m);
        Assert.Equal(250.5m, book.Price);
    }

    [Fact]
    public void ChangePrice_NonPositive_Throws()
        => Assert.Throws<DomainException>(() => NewBook().ChangePrice(0));

    [Fact]
    public void RemoveCopies_Enough_DecreasesStock()
    {
        var book = NewBook(copies: 5);
        book.RemoveCopies(2);
        Assert.Equal(3, book.AvailableCopies);
    }

    [Fact]
    public void RemoveCopies_MoreThanAvailable_Throws()
        => Assert.Throws<DomainException>(() => NewBook(copies: 1).RemoveCopies(2));

    [Fact]
    public void AddAuthor_SameAuthorTwice_Throws()
    {
        var book = NewBook();
        var author = new Author("Іван Франко");
        book.AddAuthor(author);
        Assert.Throws<DomainException>(() => book.AddAuthor(author));
    }
}

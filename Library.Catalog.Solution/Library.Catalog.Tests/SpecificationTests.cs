using Library.Catalog.Dal.Specifications;
using Library.Catalog.Domain.Entities;

namespace Library.Catalog.Tests;

/// <summary>
/// Специфікації не залежать від EF: їх можна застосувати до звичайної колекції в пам'яті.
/// Обидві специфікації проходять через один і той самий SpecificationEvaluator / WithSpecification().
/// </summary>
public class SpecificationTests
{
    private static List<Book> Books() => new()
    {
        new Book("978-966-03-0001-1", "Альфа", 100m, 1, 1, 1),
        new Book("978-966-03-0002-8", "Бета", 200m, 1, 2, 1),
        new Book("978-966-03-0003-5", "Гамма", 300m, 1, 2, 1),
        new Book("978-966-03-0004-2", "Дельта", 400m, 1, 2, 1),
    };

    [Fact]
    public void BooksFilterSpec_FiltersByPrice_SortsDescending_AndPages()
    {
        // ціна ≥ 200 → Бета, Гамма, Дельта; за ціною спадно → Дельта, Гамма, Бета; сторінка 2 по 2 → Бета
        var spec = new BooksFilterSpec(null, 200m, null, null, null, "price", "desc", page: 2, pageSize: 2);

        var result = Books().AsQueryable().WithSpecification(spec).ToList();

        Assert.Single(result);
        Assert.Equal("Бета", result[0].Title);
    }

    [Fact]
    public void BooksFilterSpec_Search_FiltersByTitle()
    {
        var spec = new BooksFilterSpec("Гам", null, null, null, null, null, null, page: 1, pageSize: 10);

        var result = Books().AsQueryable().WithSpecification(spec).ToList();

        Assert.Equal(new[] { "Гамма" }, result.Select(b => b.Title));
    }

    [Fact]
    public void BooksByCategorySpec_IsAnotherCriteria_WithSameEvaluator()
    {
        var result = Books().AsQueryable().WithSpecification(new BooksByCategorySpec(2)).ToList();

        Assert.Equal(new[] { "Бета", "Гамма", "Дельта" }, result.Select(b => b.Title));
    }
}

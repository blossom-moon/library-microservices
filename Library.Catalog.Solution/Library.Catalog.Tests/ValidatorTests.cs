using Library.Catalog.Bll.Dto;
using Library.Catalog.Bll.Validators;

namespace Library.Catalog.Tests;

public class ValidatorTests
{
    private static CreateBookDto ValidDto() => new()
    {
        Isbn = "978-966-03-0099-1",
        Title = "Тест",
        Price = 100m,
        AvailableCopies = 3,
        PublisherId = 1,
        CategoryId = 1,
        Annotation = "Анотація",
        PageCount = 100,
        Language = "uk",
    };

    [Fact]
    public void CreateBook_Valid_Passes()
        => Assert.True(new CreateBookDtoValidator().Validate(ValidDto()).IsValid);

    [Theory]
    [InlineData("123")]
    [InlineData("abcdefghij")]
    public void CreateBook_BadIsbn_FailsOnIsbn(string isbn)
    {
        var dto = ValidDto();
        dto.Isbn = isbn;
        var result = new CreateBookDtoValidator().Validate(dto);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateBookDto.Isbn));
    }

    [Fact]
    public void CreateBook_ZeroPrice_FailsOnPrice()
    {
        var dto = ValidDto();
        dto.Price = 0;
        var result = new CreateBookDtoValidator().Validate(dto);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateBookDto.Price));
    }

    [Fact]
    public void BookQuery_UnknownSortField_Fails()
    {
        var result = new BookQueryValidator().Validate(new BookQuery { SortBy = "color" });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void BookQuery_Defaults_Pass()
        => Assert.True(new BookQueryValidator().Validate(new BookQuery()).IsValid);
}

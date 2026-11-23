using Library.Reviews.Domain.Entities;
using Library.Reviews.Domain.Exceptions;
using Library.Reviews.Domain.ValueObjects;
using Xunit;

namespace Library.Reviews.Tests;

public class DomainTests
{
    private static readonly Author Olena = new("u1", "Олена");

    [Fact]
    public void Review_with_rating_10_throws_DomainException()
        => Assert.Throws<DomainException>(() => new Review(1, "Кобзар", Olena, 10, "text"));

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Review_with_rating_out_of_range_throws(int rating)
        => Assert.Throws<DomainException>(() => new Review(1, "Кобзар", Olena, rating, "text"));

    [Fact]
    public void Review_with_valid_data_is_created()
    {
        var r = new Review(1, "Кобзар", Olena, 5, "Чудово");
        Assert.Equal(5, r.Rating);
        Assert.Equal(0, r.Version);
        Assert.Empty(r.Comments);
    }

    [Fact]
    public void Value_objects_with_same_values_are_equal()
    {
        Assert.Equal(new Author("u1", "Олена"), new Author("u1", "Олена"));
        Assert.NotEqual(new Author("u1", "Олена"), new Author("u2", "Олена"));
    }

    [Fact]
    public void AddComment_appends_and_touches_entity()
    {
        var r = new Review(1, "Кобзар", Olena, 4, "ok");
        r.AddComment(new Author("u2", "Андрій"), "Згоден");
        Assert.Single(r.Comments);
        Assert.NotNull(r.UpdatedAt);
    }

    [Fact]
    public void Update_with_invalid_rating_throws()
    {
        var r = new Review(1, "Кобзар", Olena, 4, "ok");
        Assert.Throws<DomainException>(() => r.Update(11, "ok"));
    }
}

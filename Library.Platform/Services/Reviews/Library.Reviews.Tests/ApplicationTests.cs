using FluentValidation;
using Library.Reviews.Application.Common.Behaviors;
using Library.Reviews.Application.Reviews.Commands.CreateReview;
using Library.Reviews.Application.Reviews.Commands.DeleteReview;
using Library.Reviews.Domain.Entities;
using Library.Reviews.Domain.Interfaces;
using MediatR;
using MongoDB.Bson;
using Xunit;

namespace Library.Reviews.Tests;

public class ApplicationTests
{
    private sealed class FakeRepo : IReviewRepository
    {
        public bool Exists { get; set; }
        public Task<Review?> GetByIdAsync(ObjectId id, CancellationToken ct) => Task.FromResult<Review?>(null);
        public Task<bool> ExistsAsync(long bookId, string authorId, CancellationToken ct) => Task.FromResult(Exists);
        public Task AddAsync(Review review, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateAsync(Review review, CancellationToken ct) => Task.CompletedTask;
        public Task<bool> DeleteAsync(ObjectId id, CancellationToken ct) => Task.FromResult(true);
    }

    private static CreateReviewCommand Cmd(int rating = 5)
        => new(1, "Кобзар", "u1", "Олена", rating, "Чудово");

    [Fact]
    public async Task Create_validator_rejects_rating_10()
    {
        var result = await new CreateReviewCommandValidator(new FakeRepo()).ValidateAsync(Cmd(10));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateReviewCommand.Rating));
    }

    [Fact]
    public async Task Create_validator_rejects_duplicate_review_using_async_rule()
    {
        var result = await new CreateReviewCommandValidator(new FakeRepo { Exists = true }).ValidateAsync(Cmd());
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("already reviewed"));
    }

    [Fact]
    public async Task Create_validator_accepts_valid_command()
    {
        var result = await new CreateReviewCommandValidator(new FakeRepo()).ValidateAsync(Cmd());
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("not-an-objectid")]
    [InlineData("")]
    public async Task Delete_validator_rejects_invalid_ObjectId(string id)
    {
        var result = await new DeleteReviewCommandValidator().ValidateAsync(new DeleteReviewCommand(id));
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Delete_validator_accepts_valid_ObjectId()
    {
        var result = await new DeleteReviewCommandValidator()
            .ValidateAsync(new DeleteReviewCommand(ObjectId.GenerateNewId().ToString()));
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidationBehavior_collects_all_errors_and_does_not_call_handler()
    {
        var behavior = new ValidationBehavior<CreateReviewCommand, string>(
            new IValidator<CreateReviewCommand>[] { new CreateReviewCommandValidator(new FakeRepo()) });
        var handlerCalled = false;

        var bad = new CreateReviewCommand(0, "", "u1", "Олена", 10, "");   // кілька помилок одразу

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            behavior.Handle(bad, () => { handlerCalled = true; return Task.FromResult("id"); }, CancellationToken.None));

        Assert.False(handlerCalled);
        Assert.True(ex.Errors.Count() > 1);
    }

    [Fact]
    public async Task ValidationBehavior_calls_handler_when_valid()
    {
        var behavior = new ValidationBehavior<CreateReviewCommand, string>(
            new IValidator<CreateReviewCommand>[] { new CreateReviewCommandValidator(new FakeRepo()) });

        var result = await behavior.Handle(Cmd(), () => Task.FromResult("new-id"), CancellationToken.None);

        Assert.Equal("new-id", result);
    }
}

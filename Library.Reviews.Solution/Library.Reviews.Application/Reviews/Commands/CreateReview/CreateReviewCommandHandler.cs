using Library.Reviews.Domain.Entities;
using Library.Reviews.Domain.Interfaces;
using Library.Reviews.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Library.Reviews.Application.Reviews.Commands.CreateReview;

public class CreateReviewCommandHandler : IRequestHandler<CreateReviewCommand, string>
{
    private readonly IReviewRepository _reviews;
    private readonly ILogger<CreateReviewCommandHandler> _logger;

    public CreateReviewCommandHandler(IReviewRepository reviews, ILogger<CreateReviewCommandHandler> logger)
    {
        _reviews = reviews;
        _logger = logger;
    }

    public async Task<string> Handle(CreateReviewCommand cmd, CancellationToken ct)
    {
        _logger.LogInformation("Executing CreateReviewCommandHandler for book {BookId}", cmd.BookId);

        var review = new Review(cmd.BookId, cmd.BookTitle, new Author(cmd.AuthorId, cmd.AuthorName), cmd.Rating, cmd.Text);
        await _reviews.AddAsync(review, ct);

        return review.Id.ToString();
    }
}

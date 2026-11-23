using Library.Reviews.Application.Common.Messaging;

namespace Library.Reviews.Application.Reviews.Commands.CreateReview;

public record CreateReviewCommand(
    long BookId, string BookTitle, string AuthorId, string AuthorName, int Rating, string Text)
    : ICommand<string>;

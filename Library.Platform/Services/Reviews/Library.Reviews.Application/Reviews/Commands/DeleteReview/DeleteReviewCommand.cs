using Library.Reviews.Application.Common.Messaging;

namespace Library.Reviews.Application.Reviews.Commands.DeleteReview;

public record DeleteReviewCommand(string Id) : ICommand;

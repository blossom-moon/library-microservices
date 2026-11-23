using Library.Reviews.Application.Common.Messaging;

namespace Library.Reviews.Application.Reviews.Commands.UpdateReview;

/// <param name="ExpectedVersion">Очікувана версія документа (з If-Match); null — без явної перевірки.</param>
public record UpdateReviewCommand(string Id, int Rating, string Text, int? ExpectedVersion = null)
    : ICommand<ReviewDto>;

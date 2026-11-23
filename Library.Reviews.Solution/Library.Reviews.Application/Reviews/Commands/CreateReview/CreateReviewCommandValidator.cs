using FluentValidation;
using Library.Reviews.Domain.Interfaces;

namespace Library.Reviews.Application.Reviews.Commands.CreateReview;

public class CreateReviewCommandValidator : AbstractValidator<CreateReviewCommand>
{
    public CreateReviewCommandValidator(IReviewRepository reviews)
    {
        RuleFor(x => x.BookId).GreaterThan(0);
        RuleFor(x => x.BookTitle).NotEmpty().MaximumLength(300);
        RuleFor(x => x.AuthorId).NotEmpty().MaximumLength(50);
        RuleFor(x => x.AuthorName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.Text).NotEmpty().MaximumLength(2000);

        // Асинхронне правило із запитом до MongoDB: один відгук від користувача на книгу.
        // Виконується лише коли ключові поля вже коректні (зайвого звернення до БД немає).
        RuleFor(x => x.AuthorId)
            .MustAsync(async (cmd, authorId, ct) => !await reviews.ExistsAsync(cmd.BookId, authorId, ct))
            .When(x => x.BookId > 0 && !string.IsNullOrWhiteSpace(x.AuthorId))
            .WithMessage("You have already reviewed this book.");
    }
}

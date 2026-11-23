using FluentValidation;
using Library.Reviews.Application.Common.Validation;

namespace Library.Reviews.Application.Reviews.Commands.UpdateReview;

public class UpdateReviewCommandValidator : AbstractValidator<UpdateReviewCommand>
{
    public UpdateReviewCommandValidator()
    {
        RuleFor(x => x.Id).MustBeObjectId();
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.Text).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0).When(x => x.ExpectedVersion.HasValue);
    }
}

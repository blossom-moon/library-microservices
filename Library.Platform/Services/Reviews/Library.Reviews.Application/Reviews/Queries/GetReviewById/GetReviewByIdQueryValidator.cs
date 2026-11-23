using FluentValidation;
using Library.Reviews.Application.Common.Validation;

namespace Library.Reviews.Application.Reviews.Queries.GetReviewById;

public class GetReviewByIdQueryValidator : AbstractValidator<GetReviewByIdQuery>
{
    public GetReviewByIdQueryValidator() => RuleFor(x => x.Id).MustBeObjectId();
}

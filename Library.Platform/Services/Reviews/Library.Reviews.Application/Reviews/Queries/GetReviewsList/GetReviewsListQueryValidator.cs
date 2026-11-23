using FluentValidation;

namespace Library.Reviews.Application.Reviews.Queries.GetReviewsList;

public class GetReviewsListQueryValidator : AbstractValidator<GetReviewsListQuery>
{
    public GetReviewsListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).GreaterThanOrEqualTo(1);   // верхню межу (100) застосовує обробник
        RuleFor(x => x.BookId).GreaterThan(0).When(x => x.BookId.HasValue);
    }
}

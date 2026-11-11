using FluentValidation;
using Library.Catalog.Bll.Dto;

namespace Library.Catalog.Bll.Validators;

public class ChangePriceDtoValidator : AbstractValidator<ChangePriceDto>
{
    public ChangePriceDtoValidator()
    {
        RuleFor(x => x.Price).GreaterThan(0).LessThanOrEqualTo(100_000);
    }
}

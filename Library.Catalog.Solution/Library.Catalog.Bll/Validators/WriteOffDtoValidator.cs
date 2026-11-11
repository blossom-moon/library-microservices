using FluentValidation;
using Library.Catalog.Bll.Dto;

namespace Library.Catalog.Bll.Validators;

public class WriteOffDtoValidator : AbstractValidator<WriteOffDto>
{
    public WriteOffDtoValidator()
    {
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}

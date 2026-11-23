using FluentValidation;
using Library.Catalog.Bll.Dto;

namespace Library.Catalog.Bll.Validators;

public class RenameBookDtoValidator : AbstractValidator<RenameBookDto>
{
    public RenameBookDtoValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
    }
}

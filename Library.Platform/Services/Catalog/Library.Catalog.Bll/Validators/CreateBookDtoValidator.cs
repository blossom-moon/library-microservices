using FluentValidation;
using Library.Catalog.Bll.Dto;

namespace Library.Catalog.Bll.Validators;

/// <summary>Складні правила; прості (Required, StringLength) лишаються в DataAnnotations на DTO.</summary>
public class CreateBookDtoValidator : AbstractValidator<CreateBookDto>
{
    public CreateBookDtoValidator()
    {
        RuleFor(x => x.Isbn)
            .Must(IsValidIsbnFormat)
            .WithMessage("ISBN must contain 10 or 13 digits (hyphens are allowed).");

        RuleFor(x => x.Price).GreaterThan(0).LessThanOrEqualTo(100_000);
        RuleFor(x => x.AvailableCopies).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PageCount).GreaterThan(0);
        RuleFor(x => x.PublisherId).GreaterThan(0);
        RuleFor(x => x.CategoryId).GreaterThan(0);

        RuleForEach(x => x.Authors).SetValidator(new BookAuthorInputDtoValidator());
    }

    private static bool IsValidIsbnFormat(string? isbn)
    {
        if (string.IsNullOrWhiteSpace(isbn)) return true;   // обов'язковість перевіряє [Required]
        var digits = isbn.Replace("-", "");
        if (digits.Length != 10 && digits.Length != 13) return false;
        return digits.Take(digits.Length - 1).All(char.IsDigit)
               && (char.IsDigit(digits[^1]) || (digits.Length == 10 && (digits[^1] == 'X' || digits[^1] == 'x')));
    }
}

public class BookAuthorInputDtoValidator : AbstractValidator<BookAuthorInputDto>
{
    public BookAuthorInputDtoValidator()
    {
        RuleFor(x => x.AuthorId).GreaterThan(0);
        RuleFor(x => x.Role).NotEmpty().MaximumLength(50);
    }
}

using FluentValidation;
using Library.Catalog.Bll.Dto;
using Library.Catalog.Dal.Specifications;

namespace Library.Catalog.Bll.Validators;

public class BookQueryValidator : AbstractValidator<BookQuery>
{
    public BookQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).GreaterThanOrEqualTo(1);   // верхню межу (100) застосовує сервіс

        RuleFor(x => x.MinPrice).GreaterThanOrEqualTo(0).When(x => x.MinPrice.HasValue);
        RuleFor(x => x.MaxPrice).GreaterThanOrEqualTo(0).When(x => x.MaxPrice.HasValue);
        RuleFor(x => x)
            .Must(x => x.MinPrice is null || x.MaxPrice is null || x.MinPrice <= x.MaxPrice)
            .OverridePropertyName(nameof(BookQuery.MinPrice))
            .WithMessage("MinPrice must not be greater than MaxPrice.");

        RuleFor(x => x.SortBy)
            .Must(s => BooksFilterSpec.AllowedSortFields.Contains(s!.ToLowerInvariant()))
            .When(x => !string.IsNullOrWhiteSpace(x.SortBy))
            .WithMessage($"SortBy must be one of: {string.Join(", ", BooksFilterSpec.AllowedSortFields)}.");

        RuleFor(x => x.SortDir)
            .Must(s => s!.Equals("asc", StringComparison.OrdinalIgnoreCase) || s.Equals("desc", StringComparison.OrdinalIgnoreCase))
            .When(x => !string.IsNullOrWhiteSpace(x.SortDir))
            .WithMessage("SortDir must be 'asc' or 'desc'.");
    }
}

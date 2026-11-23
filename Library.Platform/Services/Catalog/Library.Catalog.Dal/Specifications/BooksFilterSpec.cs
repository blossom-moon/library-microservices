using Library.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Library.Catalog.Dal.Specifications;

/// <summary>
/// Фільтрація + сортування + пагінація списку книг. Усе виконується в SQL
/// (WHERE / ORDER BY / OFFSET ... FETCH), у пам'ять потрапляє лише одна сторінка.
/// </summary>
public class BooksFilterSpec : Specification<Book>
{
    public static readonly string[] AllowedSortFields = { "title", "price", "isbn", "copies" };

    public BooksFilterSpec(
        string? search,
        decimal? minPrice,
        decimal? maxPrice,
        long? categoryId,
        long? publisherId,
        string? sortBy,
        string? sortDir,
        int page,
        int pageSize)
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            AddCriteria(b => b.Title.Contains(term) || b.Isbn.Contains(term));
        }
        if (minPrice is not null) AddCriteria(b => b.Price >= minPrice);
        if (maxPrice is not null) AddCriteria(b => b.Price <= maxPrice);
        if (categoryId is not null) AddCriteria(b => b.CategoryId == categoryId);
        if (publisherId is not null) AddCriteria(b => b.PublisherId == publisherId);

        AddInclude(q => q.Include(b => b.Publisher));
        AddInclude(q => q.Include(b => b.Category));

        var desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
        switch (sortBy?.ToLowerInvariant())   // лише дозволені поля сортування
        {
            case "price":
                if (desc) ApplyOrderByDescending(b => b.Price); else ApplyOrderBy(b => b.Price);
                break;
            case "isbn":
                if (desc) ApplyOrderByDescending(b => b.Isbn); else ApplyOrderBy(b => b.Isbn);
                break;
            case "copies":
                if (desc) ApplyOrderByDescending(b => b.AvailableCopies); else ApplyOrderBy(b => b.AvailableCopies);
                break;
            default:
                if (desc) ApplyOrderByDescending(b => b.Title); else ApplyOrderBy(b => b.Title);
                break;
        }

        ApplyPaging((page - 1) * pageSize, pageSize);
        AsNoTracking();
    }
}

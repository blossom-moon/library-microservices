using Library.Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Library.Catalog.Dal.Specifications;

/// <summary>
/// Друга специфікація з іншими критеріями: книги однієї категорії, відсортовані за назвою.
/// Застосовується тим самим ListAsync / WithSpecification() — без змін у репозиторії.
/// </summary>
public class BooksByCategorySpec : Specification<Book>
{
    public BooksByCategorySpec(long categoryId)
    {
        AddCriteria(b => b.CategoryId == categoryId);
        AddInclude(q => q.Include(b => b.Publisher));
        AddInclude(q => q.Include(b => b.Category));
        ApplyOrderBy(b => b.Title);
        AsNoTracking();
    }
}

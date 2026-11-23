using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace Library.Catalog.Dal.Specifications;

/// <summary>
/// Специфікація описує критерії вибірки (Where, Include, OrderBy, Skip/Take) окремо від
/// репозиторію. Репозиторій лише застосовує її через WithSpecification().
/// </summary>
public abstract class Specification<T> where T : class
{
    public List<Expression<Func<T, bool>>> Criteria { get; } = new();
    public List<Func<IQueryable<T>, IQueryable<T>>> Includes { get; } = new();
    public Expression<Func<T, object>>? OrderBy { get; private set; }
    public Expression<Func<T, object>>? OrderByDescending { get; private set; }
    public int? Skip { get; private set; }
    public int? Take { get; private set; }
    public bool IsNoTracking { get; private set; }

    protected void AddCriteria(Expression<Func<T, bool>> criteria) => Criteria.Add(criteria);

    protected void AddInclude(Func<IQueryable<T>, IQueryable<T>> include) => Includes.Add(include);

    protected void ApplyOrderBy(Expression<Func<T, object>> orderBy)
    {
        OrderBy = orderBy;
        OrderByDescending = null;
    }

    protected void ApplyOrderByDescending(Expression<Func<T, object>> orderByDescending)
    {
        OrderByDescending = orderByDescending;
        OrderBy = null;
    }

    protected void ApplyPaging(int skip, int take)
    {
        Skip = skip;
        Take = take;
    }

    protected void AsNoTracking() => IsNoTracking = true;
}

public static class SpecificationEvaluator
{
    /// <param name="criteriaOnly">
    /// true — застосувати лише Where (для COUNT: без Include, OrderBy та Skip/Take).
    /// </param>
    public static IQueryable<T> GetQuery<T>(IQueryable<T> input, Specification<T> spec, bool criteriaOnly = false)
        where T : class
    {
        var query = input;

        foreach (var criteria in spec.Criteria)
            query = query.Where(criteria);

        if (criteriaOnly)
            return query;

        foreach (var include in spec.Includes)
            query = include(query);

        if (spec.OrderBy is not null)
            query = query.OrderBy(spec.OrderBy);
        else if (spec.OrderByDescending is not null)
            query = query.OrderByDescending(spec.OrderByDescending);

        if (spec.Skip is not null)
            query = query.Skip(spec.Skip.Value);
        if (spec.Take is not null)
            query = query.Take(spec.Take.Value);

        if (spec.IsNoTracking)
            query = query.AsNoTracking();

        return query;
    }
}

public static class QueryableSpecificationExtensions
{
    /// <summary>Застосовує специфікацію до IQueryable: _context.Books.WithSpecification(spec).</summary>
    public static IQueryable<T> WithSpecification<T>(this IQueryable<T> source, Specification<T> spec)
        where T : class
        => SpecificationEvaluator.GetQuery(source, spec);
}

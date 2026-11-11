using Library.Catalog.Dal.Interfaces;
using Library.Catalog.Dal.Specifications;
using Microsoft.EntityFrameworkCore;

namespace Library.Catalog.Dal.Repositories;

public class GenericRepository<T> : IGenericRepository<T> where T : class
{
    protected readonly CatalogDbContext Context;
    protected readonly DbSet<T> Set;

    public GenericRepository(CatalogDbContext context)
    {
        Context = context;
        Set = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(long id, CancellationToken ct = default)
        => await Set.FindAsync(new object[] { id }, ct);

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
        => await Set.AsNoTracking().ToListAsync(ct);

    public async Task AddAsync(T entity, CancellationToken ct = default)
        => await Set.AddAsync(entity, ct);

    public void Update(T entity) => Set.Update(entity);

    public void Delete(T entity) => Set.Remove(entity);

    public async Task<IReadOnlyList<T>> ListAsync(Specification<T> spec, CancellationToken ct = default)
        => await Set.WithSpecification(spec).ToListAsync(ct);

    public Task<int> CountAsync(Specification<T> spec, CancellationToken ct = default)
        => SpecificationEvaluator.GetQuery(Set, spec, criteriaOnly: true).CountAsync(ct);
}

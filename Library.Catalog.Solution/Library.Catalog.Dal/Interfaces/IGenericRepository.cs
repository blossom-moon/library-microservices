using Library.Catalog.Dal.Specifications;

namespace Library.Catalog.Dal.Interfaces;

/// <summary>
/// Async Generic Repository над DbSet&lt;T&gt;. Update/Delete лише змінюють стан у контексті;
/// зберігає зміни виключно Unit of Work (єдиний SaveChangesAsync).
/// </summary>
public interface IGenericRepository<T> where T : class
{
    Task<T?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Delete(T entity);

    /// <summary>Вибірка за специфікацією (Where/Include/OrderBy/Skip/Take).</summary>
    Task<IReadOnlyList<T>> ListAsync(Specification<T> spec, CancellationToken ct = default);

    /// <summary>Кількість записів, що задовольняють критеріям специфікації (без пагінації).</summary>
    Task<int> CountAsync(Specification<T> spec, CancellationToken ct = default);
}

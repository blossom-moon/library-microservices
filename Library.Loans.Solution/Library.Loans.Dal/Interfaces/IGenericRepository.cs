namespace Library.Loans.Dal.Interfaces;

/// <summary>
/// Мінімальний спільний набір CRUD-операцій для репозиторіїв, що читають/пишуть
/// через Dapper (BaseDapperRepository&lt;T&gt;). Репозиторій на чистому ADO.NET
/// (ReaderRepository) свідомо НЕ реалізує цей інтерфейс — див. Лаб.02, крок B.2:
/// там важливо показати керування SqlCommand/SqlDataReader вручну, а не ховати
/// його за узагальненим контрактом.
/// </summary>
public interface IGenericRepository<T> where T : class
{
    Task<T?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task<long> AddAsync(T entity, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
}

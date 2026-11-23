namespace Library.Catalog.Bll.Caching;

/// <summary>Двохрівневий кеш: L1 (IMemoryCache) → L2 (Redis через IDistributedCache) → база даних.</summary>
public interface ITwoLevelCache
{
    /// <summary>Cache-aside: L1 → L2 → factory (БД); кожен рівень заповнюється на зворотному шляху.</summary>
    Task<T> GetOrCreateAsync<T>(string key, CachePolicy policy, Func<CancellationToken, Task<T>> factory, CancellationToken ct)
        where T : class;

    /// <summary>Видаляє запис з обох рівнів; reason потрапляє в лог (наприклад, "BookPriceChanged").</summary>
    Task RemoveAsync(string key, string reason, CancellationToken ct = default);
}

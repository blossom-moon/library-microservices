using Microsoft.Extensions.Caching.Memory;

namespace Library.Catalog.Bll.Caching;

/// <summary>
/// Політика часу життя запису для обох рівнів. Правило: L1 (пам'ять) живе коротше за L2 (Redis),
/// інакше застарілі дані в пам'яті переживуть оновлення в Redis — конструктор це перевіряє.
/// </summary>
public sealed record CachePolicy
{
    public CachePolicy(TimeSpan? l1Sliding, TimeSpan l1Absolute, TimeSpan l2Absolute,
        CacheItemPriority priority = CacheItemPriority.Normal)
    {
        if (l1Absolute >= l2Absolute)
            throw new ArgumentException("L1 TTL must be shorter than L2 TTL.", nameof(l1Absolute));
        if (l1Sliding is { } sliding && sliding > l1Absolute)
            throw new ArgumentException("L1 sliding expiration cannot exceed the absolute one.", nameof(l1Sliding));

        L1Sliding = l1Sliding;
        L1Absolute = l1Absolute;
        L2Absolute = l2Absolute;
        Priority = priority;
    }

    /// <summary>Ковзне вікно L1 — для часто запитуваних записів (null — не використовується).</summary>
    public TimeSpan? L1Sliding { get; }
    public TimeSpan L1Absolute { get; }
    public TimeSpan L2Absolute { get; }
    /// <summary>Пріоритет витіснення L1 при досягненні SizeLimit: High витісняється останнім.</summary>
    public CacheItemPriority Priority { get; }
}

/// <summary>TTL за типом даних.</summary>
public static class CachePolicies
{
    /// <summary>Картка книги: читається часто → ковзне вікно (sliding) + абсолютний ліміт.</summary>
    public static readonly CachePolicy Product =
        new(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(10));

    /// <summary>Книги категорії (довідникові дані, змінюються рідко) → лише абсолютний термін, вищий пріоритет.</summary>
    public static readonly CachePolicy CategoryBooks =
        new(null, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(15), CacheItemPriority.High);
}

/// <summary>
/// Схема імен ключів: "{сервіс}:{сутність}:{id}[:{що}]" — префікс ізолює ключі сервісів у спільному Redis
/// і дозволяє інвалідувати пов'язані записи за відомим ключем. «Product» — це книга каталогу.
/// </summary>
public static class CacheKeys
{
    public static string Product(long id) => $"catalog:product:{id}";
    public static string CategoryBooks(long categoryId) => $"catalog:category:{categoryId}:products";
}

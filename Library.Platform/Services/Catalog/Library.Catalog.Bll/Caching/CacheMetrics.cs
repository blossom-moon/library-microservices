using System.Diagnostics.Metrics;
using Microsoft.Extensions.Caching.Memory;

namespace Library.Catalog.Bll.Caching;

/// <summary>Миттєвий знімок лічильників — для health check.</summary>
public sealed record CacheStatsSnapshot(long L1Hits, long L1Misses, long L2Hits, long L2Misses, long Invalidations)
{
    public double L1HitRatio => Ratio(L1Hits, L1Misses);
    public double L2HitRatio => Ratio(L2Hits, L2Misses);
    private static double Ratio(long hits, long misses) => hits + misses == 0 ? 0 : Math.Round((double)hits / (hits + misses), 3);
}

/// <summary>
/// Метрики кешу (Meter "Library.Catalog.Cache"; підписка — AddMeter у ServiceDefaults):
/// cache.hits / cache.misses з тегом cache.level (L1/L2) → hit ratio окремо для пам'яті та Redis,
/// cache.invalidations, cache.evictions (з причиною), cache.errors, тривалість операцій,
/// розмір та кількість записів L1 (gauge).
/// </summary>
public sealed class CacheMetrics
{
    public const string MeterName = "Library.Catalog.Cache";

    private readonly Counter<long> _hits;
    private readonly Counter<long> _misses;
    private readonly Counter<long> _invalidations;
    private readonly Counter<long> _evictions;
    private readonly Counter<long> _errors;
    private readonly Histogram<double> _duration;

    private long _l1Hits, _l1Misses, _l2Hits, _l2Misses, _invalidationCount;

    public CacheMetrics(IMeterFactory meterFactory, IMemoryCache memoryCache)
    {
        var meter = meterFactory.Create(MeterName);
        _hits = meter.CreateCounter<long>("cache.hits", description: "Cache hits by level");
        _misses = meter.CreateCounter<long>("cache.misses", description: "Cache misses by level");
        _invalidations = meter.CreateCounter<long>("cache.invalidations", description: "Explicit invalidations after writes");
        _evictions = meter.CreateCounter<long>("cache.evictions", description: "L1 evictions (capacity / expiration)");
        _errors = meter.CreateCounter<long>("cache.errors", description: "L2 (Redis) errors; the service falls back to the database");
        _duration = meter.CreateHistogram<double>("cache.operation.duration", unit: "ms", description: "Duration of cache lookups by level");

        // Розмір L1 в «одиницях Size» (KiB) та кількість записів (потрібен TrackStatistics = true)
        meter.CreateObservableGauge("cache.l1.size", () => Stats(memoryCache)?.CurrentEstimatedSize ?? 0, unit: "KiB",
            description: "Estimated L1 size");
        meter.CreateObservableGauge("cache.l1.entries", () => Stats(memoryCache)?.CurrentEntryCount ?? 0,
            description: "Number of entries in L1");
    }

    private static MemoryCacheStatistics? Stats(IMemoryCache cache) => (cache as MemoryCache)?.GetCurrentStatistics();

    public void Hit(string level)
    {
        _hits.Add(1, new KeyValuePair<string, object?>("cache.level", level));
        if (level == "L1") Interlocked.Increment(ref _l1Hits); else Interlocked.Increment(ref _l2Hits);
    }

    public void Miss(string level)
    {
        _misses.Add(1, new KeyValuePair<string, object?>("cache.level", level));
        if (level == "L1") Interlocked.Increment(ref _l1Misses); else Interlocked.Increment(ref _l2Misses);
    }

    public void Invalidated()
    {
        _invalidations.Add(1);
        Interlocked.Increment(ref _invalidationCount);
    }

    public void Evicted(string reason) => _evictions.Add(1, new KeyValuePair<string, object?>("cache.eviction.reason", reason));

    public void Error(string level) => _errors.Add(1, new KeyValuePair<string, object?>("cache.level", level));

    public void Duration(string level, string result, double milliseconds) =>
        _duration.Record(milliseconds,
            new KeyValuePair<string, object?>("cache.level", level),
            new KeyValuePair<string, object?>("cache.result", result));

    public CacheStatsSnapshot Snapshot() => new(
        Interlocked.Read(ref _l1Hits), Interlocked.Read(ref _l1Misses),
        Interlocked.Read(ref _l2Hits), Interlocked.Read(ref _l2Misses),
        Interlocked.Read(ref _invalidationCount));
}

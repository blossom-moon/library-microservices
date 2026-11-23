using System.Diagnostics.Metrics;

namespace Platform.Aggregator.Caching;

/// <summary>
/// Метрики кешу агрегованих відповідей (Meter "Platform.Aggregator.Cache"; підписка — AddMeter у ServiceDefaults).
/// Ті самі імена інструментів, що й у Каталозі: cache.hits / cache.misses з тегом cache.level.
/// </summary>
public sealed class AggregatorCacheMetrics
{
    public const string MeterName = "Platform.Aggregator.Cache";
    private const string Level = "L2";   // агрегований результат живе лише в Redis (спільний для всіх екземплярів)

    private readonly Counter<long> _hits;
    private readonly Counter<long> _misses;
    private readonly Counter<long> _errors;

    public AggregatorCacheMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _hits = meter.CreateCounter<long>("cache.hits", description: "Cache hits by level");
        _misses = meter.CreateCounter<long>("cache.misses", description: "Cache misses by level");
        _errors = meter.CreateCounter<long>("cache.errors", description: "Redis errors; the aggregator falls back to gRPC calls");
    }

    public void Hit() => _hits.Add(1, new KeyValuePair<string, object?>("cache.level", Level));
    public void Miss() => _misses.Add(1, new KeyValuePair<string, object?>("cache.level", Level));
    public void Error() => _errors.Add(1, new KeyValuePair<string, object?>("cache.level", Level));
}

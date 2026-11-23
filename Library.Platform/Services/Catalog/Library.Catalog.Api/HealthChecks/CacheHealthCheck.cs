using Library.Catalog.Bll.Caching;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Library.Catalog.Api.HealthChecks;

/// <summary>Статистика кешу у /health: hit ratio окремо для L1 (пам'ять) і L2 (Redis), кількість інвалідацій.</summary>
public class CacheHealthCheck(CacheMetrics metrics) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var s = metrics.Snapshot();
        var data = new Dictionary<string, object>
        {
            ["l1.hits"] = s.L1Hits, ["l1.misses"] = s.L1Misses, ["l1.hitRatio"] = s.L1HitRatio,
            ["l2.hits"] = s.L2Hits, ["l2.misses"] = s.L2Misses, ["l2.hitRatio"] = s.L2HitRatio,
            ["invalidations"] = s.Invalidations
        };
        return Task.FromResult(HealthCheckResult.Healthy("Cache statistics", data));
    }
}

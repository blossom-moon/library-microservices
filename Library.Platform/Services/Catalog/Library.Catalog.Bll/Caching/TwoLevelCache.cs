using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Library.Catalog.Bll.Caching;

/// <summary>
/// Реалізація двохрівневого кешу (Лаб. 06, частина 3.3):
/// L1 — IMemoryCache (найшвидший, власний для кожного екземпляра, короткий TTL);
/// L2 — Redis через IDistributedCache (спільний для всіх екземплярів, довший TTL; DTO в System.Text.Json).
/// Redis недоступний → сервіс не падає: читає з бази (повільніше), а інвалідація, яку не вдалося виконати,
/// повторюється одразу після відновлення з'єднання.
/// </summary>
public sealed class TwoLevelCache(
    IMemoryCache memory,
    IDistributedCache distributed,
    CacheMetrics metrics,
    ILogger<TwoLevelCache> logger) : ITwoLevelCache
{
    private static readonly ActivitySource Source = new("Library.Catalog.Cache");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan L2Cooldown = TimeSpan.FromSeconds(15);

    // Після збою Redis кілька секунд не чекаємо на його таймаути при кожному запиті
    private long _l2DownUntilTicks;
    // Ключі, які не вдалося видалити з Redis: очищаються перед наступним зверненням до L2
    private readonly ConcurrentDictionary<string, byte> _pendingInvalidations = new();

    private bool L2Available => DateTime.UtcNow.Ticks >= Interlocked.Read(ref _l2DownUntilTicks);

    public async Task<T> GetOrCreateAsync<T>(string key, CachePolicy policy,
        Func<CancellationToken, Task<T>> factory, CancellationToken ct) where T : class
    {
        using var activity = Source.StartActivity("cache.get");
        activity?.SetTag("cache.key", key);
        var started = Stopwatch.GetTimestamp();

        // ---- L1: пам'ять ----
        if (memory.TryGetValue(key, out T? fromMemory) && fromMemory is not null)
        {
            metrics.Hit("L1");
            metrics.Duration("L1", "hit", Elapsed(started));
            activity?.SetTag("cache.result", "hit-l1");
            logger.LogInformation("Cache hit L1 {Key} (ttl up to {Ttl}, abs)", key, policy.L1Absolute);
            return fromMemory;
        }
        metrics.Miss("L1");

        // ---- L2: Redis ----
        if (L2Available)
        {
            try
            {
                await FlushPendingInvalidationsAsync(ct);

                var bytes = await distributed.GetAsync(key, ct);
                if (bytes is not null && JsonSerializer.Deserialize<T>(bytes, Json) is { } fromRedis)
                {
                    SetL1(key, fromRedis, bytes.Length, policy);          // L1 заповнюється на зворотному шляху
                    metrics.Hit("L2");
                    metrics.Duration("L2", "hit", Elapsed(started));
                    activity?.SetTag("cache.result", "hit-l2");
                    logger.LogInformation("Cache hit L2 {Key} (ttl {Ttl}, size {SizeBytes} bytes)",
                        key, policy.L2Absolute, bytes.Length);
                    return fromRedis;
                }

                metrics.Miss("L2");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                OnL2Failure(ex, "read", key);
            }
        }

        // ---- База даних ----
        activity?.SetTag("cache.result", "miss");
        logger.LogInformation("Cache miss {Key}: loading from the database", key);
        var value = await factory(ct);

        var payload = JsonSerializer.SerializeToUtf8Bytes(value, Json);
        if (L2Available)
        {
            try
            {
                await distributed.SetAsync(key, payload,
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = policy.L2Absolute }, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                OnL2Failure(ex, "write", key);
            }
        }
        SetL1(key, value, payload.Length, policy);
        metrics.Duration("db", "miss", Elapsed(started));
        return value;
    }

    public async Task RemoveAsync(string key, string reason, CancellationToken ct = default)
    {
        memory.Remove(key);

        // Інвалідація завжди пробує дістатися Redis — навіть у «cooldown»: застарілий запис у L2 небезпечніший за повільну відповідь
        try
        {
            await distributed.RemoveAsync(key, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _pendingInvalidations[key] = 0;
            OnL2Failure(ex, "invalidate", key);
        }

        metrics.Invalidated();
        logger.LogInformation("Cache invalidated {Key}, reason {Reason}", key, reason);
    }

    private void SetL1<T>(string key, T value, int payloadBytes, CachePolicy policy) where T : class
    {
        var options = new MemoryCacheEntryOptions
        {
            // Size — у KiB серіалізованого значення; сума обмежена SizeLimit у Program.cs
            Size = Math.Max(1, (payloadBytes + 1023) / 1024),
            SlidingExpiration = policy.L1Sliding,
            AbsoluteExpirationRelativeToNow = policy.L1Absolute,
            Priority = policy.Priority
        };
        options.RegisterPostEvictionCallback(OnEvicted);
        memory.Set(key, value, options);
    }

    private void OnEvicted(object key, object? value, EvictionReason reason, object? state)
    {
        // Removed/Replaced — наслідок явної інвалідації або перезапису, не витіснення
        if (reason is EvictionReason.Removed or EvictionReason.Replaced) return;

        metrics.Evicted(reason.ToString());
        logger.LogDebug("L1 entry {Key} evicted: {EvictionReason}", key, reason);
    }

    private async Task FlushPendingInvalidationsAsync(CancellationToken ct)
    {
        foreach (var key in _pendingInvalidations.Keys)
        {
            await distributed.RemoveAsync(key, ct);
            _pendingInvalidations.TryRemove(key, out _);
            logger.LogInformation("Cache invalidation retried after Redis recovery {Key}", key);
        }
    }

    private void OnL2Failure(Exception ex, string operation, string key)
    {
        Interlocked.Exchange(ref _l2DownUntilTicks, DateTime.UtcNow.Add(L2Cooldown).Ticks);
        metrics.Error("L2");
        logger.LogWarning(ex, "Redis (L2) {Operation} failed for {Key}; falling back to the database for {Cooldown} s",
            operation, key, L2Cooldown.TotalSeconds);
    }

    private static double Elapsed(long started) => Stopwatch.GetElapsedTime(started).TotalMilliseconds;
}

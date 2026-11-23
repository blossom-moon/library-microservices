using System.Diagnostics.Metrics;
using Library.Catalog.Bll.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Library.Catalog.Tests;

/// <summary>Двохрівневий кеш (Лаб. 06): L1 → L2 → «БД», інвалідація, деградація при падінні Redis.</summary>
public class TwoLevelCacheTests
{
    public sealed record Item(int Id, string Name);

    private sealed class TestMeterFactory : IMeterFactory
    {
        public Meter Create(MeterOptions options) => new(options);
        public void Dispose() { }
    }

    /// <summary>L2, який завжди кидає виняток — імітація зупиненого Redis.</summary>
    private sealed class BrokenDistributedCache : IDistributedCache
    {
        public byte[]? Get(string key) => throw new InvalidOperationException("Redis is down");
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw new InvalidOperationException("Redis is down");
        public void Refresh(string key) => throw new InvalidOperationException("Redis is down");
        public Task RefreshAsync(string key, CancellationToken token = default) => throw new InvalidOperationException("Redis is down");
        public void Remove(string key) => throw new InvalidOperationException("Redis is down");
        public Task RemoveAsync(string key, CancellationToken token = default) => throw new InvalidOperationException("Redis is down");
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw new InvalidOperationException("Redis is down");
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => throw new InvalidOperationException("Redis is down");
    }

    private static MemoryCache NewMemory() => new(Options.Create(new MemoryCacheOptions { SizeLimit = 1000, TrackStatistics = true }));

    private static MemoryDistributedCache NewRedis() => new(Options.Create(new MemoryDistributedCacheOptions()));

    private static (TwoLevelCache cache, CacheMetrics metrics) Build(IMemoryCache memory, IDistributedCache distributed)
    {
        var metrics = new CacheMetrics(new TestMeterFactory(), memory);
        return (new TwoLevelCache(memory, distributed, metrics, NullLogger<TwoLevelCache>.Instance), metrics);
    }

    [Fact]
    public async Task SecondRead_IsServedFromL1_WithoutCallingFactory()
    {
        var (cache, metrics) = Build(NewMemory(), NewRedis());
        var calls = 0;
        Task<Item> Factory(CancellationToken _) { calls++; return Task.FromResult(new Item(1, "Кобзар")); }

        var first = await cache.GetOrCreateAsync("catalog:product:1", CachePolicies.Product, Factory, default);
        var second = await cache.GetOrCreateAsync("catalog:product:1", CachePolicies.Product, Factory, default);

        Assert.Equal(1, calls);
        Assert.Equal(first, second);
        var stats = metrics.Snapshot();
        Assert.Equal(1, stats.L1Hits);
        Assert.Equal(1, stats.L1Misses);
    }

    [Fact]
    public async Task AnotherInstance_ReadsFromSharedL2_WithoutCallingFactory()
    {
        var redis = NewRedis();                                  // спільний L2
        var (first, _) = Build(NewMemory(), redis);              // «репліка 1»
        var (second, metrics) = Build(NewMemory(), redis);       // «репліка 2» з порожнім L1
        var calls = 0;
        Task<Item> Factory(CancellationToken _) { calls++; return Task.FromResult(new Item(1, "Кобзар")); }

        await first.GetOrCreateAsync("catalog:product:1", CachePolicies.Product, Factory, default);
        var fromL2 = await second.GetOrCreateAsync("catalog:product:1", CachePolicies.Product, Factory, default);

        Assert.Equal(1, calls);
        Assert.Equal("Кобзар", fromL2.Name);
        Assert.Equal(1, metrics.Snapshot().L2Hits);
    }

    [Fact]
    public async Task Remove_InvalidatesBothLevels_NextReadHitsFactory()
    {
        var (cache, metrics) = Build(NewMemory(), NewRedis());
        var version = 1;
        Task<Item> Factory(CancellationToken _) => Task.FromResult(new Item(1, $"v{version}"));

        var before = await cache.GetOrCreateAsync("catalog:product:1", CachePolicies.Product, Factory, default);
        version = 2;                                             // «PUT»: дані змінилися
        await cache.RemoveAsync("catalog:product:1", "BookPriceChanged");
        var after = await cache.GetOrCreateAsync("catalog:product:1", CachePolicies.Product, Factory, default);

        Assert.Equal("v1", before.Name);
        Assert.Equal("v2", after.Name);                          // не застарілі дані з кешу
        Assert.Equal(1, metrics.Snapshot().Invalidations);
    }

    [Fact]
    public async Task FactoryException_IsPropagated_AndNothingIsCached()
    {
        var (cache, _) = Build(NewMemory(), NewRedis());
        var calls = 0;
        Task<Item> Failing(CancellationToken _) { calls++; throw new KeyNotFoundException("missing"); }

        await Assert.ThrowsAsync<KeyNotFoundException>(() => cache.GetOrCreateAsync("k", CachePolicies.Product, Failing, default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => cache.GetOrCreateAsync("k", CachePolicies.Product, Failing, default));

        Assert.Equal(2, calls);                                  // «не знайдено» не кешується
    }

    [Fact]
    public async Task RedisDown_ServiceKeepsWorking_ViaDatabase()
    {
        var (cache, _) = Build(NewMemory(), new BrokenDistributedCache());
        var calls = 0;
        Task<Item> Factory(CancellationToken _) { calls++; return Task.FromResult(new Item(7, "Місто")); }

        var first = await cache.GetOrCreateAsync("catalog:product:7", CachePolicies.Product, Factory, default);
        var second = await cache.GetOrCreateAsync("catalog:product:7", CachePolicies.Product, Factory, default);

        Assert.Equal("Місто", first.Name);
        Assert.Equal(1, calls);                                  // L1 усе одно працює
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task RedisDown_RemoveDoesNotThrow()
    {
        var (cache, _) = Build(NewMemory(), new BrokenDistributedCache());
        await cache.RemoveAsync("catalog:product:1", "BookDeleted");
    }

    [Fact]
    public void Policy_L1MustBeShorterThanL2()
    {
        Assert.Throws<ArgumentException>(() => new CachePolicy(null, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10)));
        Assert.Throws<ArgumentException>(() => new CachePolicy(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(10)));
        Assert.NotNull(CachePolicies.Product);
        Assert.NotNull(CachePolicies.CategoryBooks);
    }

    [Fact]
    public void Keys_FollowNamingScheme()
    {
        Assert.Equal("catalog:product:42", CacheKeys.Product(42));
        Assert.Equal("catalog:category:3:products", CacheKeys.CategoryBooks(3));
    }
}

using System.Diagnostics;
using System.Text.Json;
using Grpc.Core;
using Library.Contracts.Catalog;
using Library.Contracts.Loans;
using Library.Contracts.Reviews;
using Microsoft.Extensions.Caching.Distributed;
using Platform.Aggregator.Caching;
using Platform.Aggregator.Mapping;
using Platform.Aggregator.Models;

namespace Platform.Aggregator.Services;

/// <summary>
/// Aggregator на gRPC (Лаб. 06): HTTP-виклики Лаб. 05 замінено типізованими gRPC-клієнтами.
/// Незалежні виклики (catalog-api і reviews-api) виконуються паралельно через Task.WhenAll і мультиплексуються
/// по HTTP/2; CancellationToken і дедлайн передаються в кожен виклик; недоступність необов'язкового сервісу
/// (RpcException Unavailable/DeadlineExceeded) → попередження у відповіді замість 500.
/// Готова агрегована відповідь кешується в Redis на 30 с (часткові відповіді не кешуються).
/// </summary>
public class LoanAggregationService(
    LoansService.LoansServiceClient loans,
    CatalogService.CatalogServiceClient catalog,
    ReviewsService.ReviewsServiceClient reviews,
    IDistributedCache cache,
    AggregatorCacheMetrics cacheMetrics,
    ILogger<LoanAggregationService> logger)
{
    private static readonly ActivitySource Source = new("Platform.Aggregator");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // Короткий TTL: агрегат не повинен виглядати «свіжішим», ніж дані окремих сервісів (їхні кеші — до 10–15 хв, але інвалідуються)
    private static readonly DistributedCacheEntryOptions CacheEntryOptions =
        new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) };

    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(5);

    public async Task<FullLoanDto?> GetFullLoanAsync(long loanId, CancellationToken ct)
    {
        using var activity = Source.StartActivity("GetFullLoan");
        activity?.SetTag("loan.id", loanId);

        // Ключ складається з параметрів запиту; кеш перевіряється ДО gRPC-викликів
        var key = $"aggregator:loan:{loanId}:full";

        var cached = await TryReadCacheAsync(key, ct);
        if (cached is not null)
        {
            activity?.SetTag("cache.result", "hit");
            return cached;
        }
        activity?.SetTag("cache.result", "miss");

        var result = await AggregateAsync(loanId, ct);

        // Часткову відповідь (з попередженнями) не кешуємо — інакше збій «застрягне» на TTL
        if (result is not null && result.Warnings.Count == 0)
            await TryWriteCacheAsync(key, result, ct);

        return result;
    }

    private async Task<FullLoanDto?> AggregateAsync(long loanId, CancellationToken ct)
    {
        var warnings = new List<string>();

        // 1) Без видачі агрегувати нічого
        LoanReply loanReply;
        try
        {
            logger.LogInformation("Calling loans-api over gRPC for loan {LoanId}", loanId);
            var sw = Stopwatch.StartNew();
            loanReply = await loans.GetLoanAsync(new GetLoanRequest { Id = loanId },
                deadline: Deadline(), cancellationToken: ct);
            logger.LogInformation("loans-api responded over gRPC in {Elapsed} ms", sw.ElapsedMilliseconds);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            logger.LogInformation("Loan {LoanId} not found in loans-api", loanId);
            return null;
        }
        catch (RpcException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "loans-api is unavailable ({GrpcStatus})", ex.StatusCode);
            throw new UpstreamUnavailableException("loans-api", ex);
        }

        var loan = loanReply.ToDto();
        var bookIds = loan.Items.Select(i => i.BookId).Distinct().ToList();

        // 2) Незалежні виклики — паралельно (один HTTP/2-канал на сервіс, виклики мультиплексуються)
        var booksTask = SafeAsync("catalog-api", warnings, ct, async () =>
        {
            var request = new GetBooksByIdsRequest();
            request.Ids.AddRange(bookIds);
            var reply = await catalog.GetBooksByIdsAsync(request, deadline: Deadline(), cancellationToken: ct);
            return (IReadOnlyList<BookDto>)reply.Books.Select(b => b.ToDto()).ToList();
        });

        var reviewsTask = SafeAsync("reviews-api", warnings, ct, async () =>
        {
            var request = new GetReviewsByBookIdsRequest { PageSize = 50 };
            request.BookIds.AddRange(bookIds);
            var reply = await reviews.GetReviewsByBookIdsAsync(request, deadline: Deadline(), cancellationToken: ct);
            return (IReadOnlyList<ReviewDto>)reply.Reviews.Select(r => r.ToDto()).ToList();
        });

        await Task.WhenAll(booksTask, reviewsTask);

        var books = await booksTask;
        var allReviews = await reviewsTask;

        // 3) Узгодженість: лише книги й відгуки саме цієї видачі
        var catalogById = (books ?? []).Where(b => bookIds.Contains(b.BookId)).ToDictionary(b => b.BookId);
        var reviewsByBook = (allReviews ?? []).Where(r => bookIds.Contains(r.BookId))
                                              .GroupBy(r => r.BookId)
                                              .ToDictionary(g => g.Key, g => (IReadOnlyList<ReviewDto>)g.ToList());

        if (books is not null)
            foreach (var missing in bookIds.Where(id => !catalogById.ContainsKey(id)))
                warnings.Add($"Book {missing} was not found in catalog-api.");

        var cards = loan.Items
            .GroupBy(i => i.BookId)
            .Select(g =>
            {
                var bookReviews = reviewsByBook.GetValueOrDefault(g.Key) ?? [];
                return new LoanBookCardDto(
                    g.Key,
                    g.First().BookTitle,
                    catalogById.GetValueOrDefault(g.Key),
                    bookReviews.Count > 0 ? Math.Round(bookReviews.Average(r => r.Rating), 2) : null,
                    bookReviews);
            })
            .ToList();

        Activity.Current?.SetTag("aggregation.warnings", warnings.Count);
        return new FullLoanDto(loan, cards, warnings);
    }

    /// <summary>Виклик необов'язкового сервісу: збій gRPC → попередження, а не помилка всього запиту.</summary>
    private async Task<T?> SafeAsync<T>(string service, List<string> warnings, CancellationToken ct, Func<Task<T>> call)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await call();
            logger.LogInformation("{Service} responded over gRPC in {Elapsed} ms", service, sw.ElapsedMilliseconds);
            return result;
        }
        catch (RpcException ex) when (!ct.IsCancellationRequested)   // скасування клієнтом — не збій сервісу
        {
            // Unavailable / DeadlineExceeded — очікувані збої; решта статусів теж не повинна давати 500
            logger.LogWarning(ex, "{Service} failed over gRPC with {GrpcStatus} after {Elapsed} ms",
                service, ex.StatusCode, sw.ElapsedMilliseconds);
            lock (warnings) warnings.Add($"{service} is unavailable ({ex.StatusCode})");
            return default;
        }
    }

    private static DateTime Deadline() => DateTime.UtcNow.Add(CallTimeout);

    // ---- Кеш: Redis недоступний → працюємо без нього (повільніше), але не падаємо ----

    private async Task<FullLoanDto?> TryReadCacheAsync(string key, CancellationToken ct)
    {
        try
        {
            var bytes = await cache.GetAsync(key, ct);
            if (bytes is null)
            {
                cacheMetrics.Miss();
                logger.LogInformation("Cache miss {Key}", key);
                return null;
            }

            var dto = JsonSerializer.Deserialize<FullLoanDto>(bytes, Json);
            cacheMetrics.Hit();
            logger.LogInformation("Cache hit {Key} (size {SizeBytes} bytes)", key, bytes.Length);
            return dto;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            cacheMetrics.Error();
            logger.LogWarning(ex, "Redis is unavailable; skipping the aggregate cache for {Key}", key);
            return null;
        }
    }

    private async Task TryWriteCacheAsync(string key, FullLoanDto result, CancellationToken ct)
    {
        try
        {
            await cache.SetAsync(key, JsonSerializer.SerializeToUtf8Bytes(result, Json), CacheEntryOptions, ct);
            logger.LogInformation("Cache set {Key} (ttl {Ttl} s)", key, CacheEntryOptions.AbsoluteExpirationRelativeToNow!.Value.TotalSeconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            cacheMetrics.Error();
            logger.LogWarning(ex, "Redis is unavailable; the aggregate was not cached for {Key}", key);
        }
    }
}

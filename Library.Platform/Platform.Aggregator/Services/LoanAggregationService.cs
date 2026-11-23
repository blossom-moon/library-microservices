using System.Diagnostics;
using Platform.Aggregator.Clients;
using Platform.Aggregator.Models;

namespace Platform.Aggregator.Services;

/// <summary>loans-api недоступний — агрегувати нічого (на відміну від необов'язкових catalog-api та reviews-api).</summary>
public class UpstreamUnavailableException(string service, Exception inner)
    : Exception($"{service} is unavailable.", inner)
{
    public string Service { get; } = service;
}

/// <summary>
/// Aggregator Pattern: одна відповідь замість трьох запитів клієнта. Незалежні виклики
/// (catalog-api і reviews-api) виконуються одночасно через Task.WhenAll; недоступність
/// необов'язкового сервісу не валить запит — відповідь містить доступні дані та Warnings.
/// </summary>
public class LoanAggregationService(
    LoansClient loans, CatalogClient catalog, ReviewsClient reviews, ILogger<LoanAggregationService> logger)
{
    private static readonly ActivitySource Source = new("Platform.Aggregator");

    public async Task<FullLoanDto?> GetFullLoanAsync(long loanId, CancellationToken ct)
    {
        using var activity = Source.StartActivity("GetFullLoan");
        activity?.SetTag("loan.id", loanId);

        var warnings = new List<string>();

        // 1) Без видачі агрегувати нічого
        LoanDto? loan;
        try
        {
            loan = await loans.GetLoanAsync(loanId, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "loans-api is unavailable");
            throw new UpstreamUnavailableException("loans-api", ex);
        }
        if (loan is null) return null;

        // 2) Незалежні виклики — паралельно
        var bookIds = loan.Items.Select(i => i.BookId).Distinct().ToList();
        var booksTask = SafeAsync(() => catalog.GetBooksAsync(bookIds, ct), "catalog-api", warnings, ct);
        var reviewsTask = SafeAsync(() => reviews.GetForBooksAsync(bookIds, ct), "reviews-api", warnings, ct);

        await Task.WhenAll(booksTask, reviewsTask);

        var books = await booksTask;
        var allReviews = await reviewsTask;

        // 3) Узгодженість: у відповідь потрапляють лише книги й відгуки саме цієї видачі
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

        activity?.SetTag("aggregation.warnings", warnings.Count);
        return new FullLoanDto(loan, cards, warnings);
    }

    /// <summary>Виклик необов'язкового сервісу: збій → попередження, а не помилка всього запиту.</summary>
    private async Task<T?> SafeAsync<T>(Func<Task<T>> call, string service, List<string> warnings, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await call();
            logger.LogInformation("{Service} responded in {Elapsed} ms", service, sw.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)   // клієнт скасував запит — це не збій сервісу
        {
            logger.LogWarning(ex, "{Service} is unavailable after {Elapsed} ms", service, sw.ElapsedMilliseconds);
            lock (warnings) warnings.Add($"{service} is unavailable");
            return default;
        }
    }
}

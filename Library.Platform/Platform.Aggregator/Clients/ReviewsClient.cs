using System.Diagnostics;
using Platform.Aggregator.Models;

namespace Platform.Aggregator.Clients;

public class ReviewsClient(HttpClient http, ILogger<ReviewsClient> logger)
{
    private const int PageSize = 50;

    /// <summary>Відгуки на книги (до 50 на книгу); запити до reviews-api — паралельні.</summary>
    public async Task<IReadOnlyList<ReviewDto>> GetForBooksAsync(IReadOnlyCollection<long> bookIds, CancellationToken ct)
    {
        logger.LogInformation("Calling reviews-api for books {BookIds}", bookIds);
        var sw = Stopwatch.StartNew();

        var pages = await Task.WhenAll(bookIds.Select(id => GetForBookAsync(id, ct)));

        logger.LogInformation("reviews-api responded for {BooksCount} books in {Elapsed} ms", bookIds.Count, sw.ElapsedMilliseconds);
        return pages.SelectMany(p => p).ToList();
    }

    private async Task<IReadOnlyList<ReviewDto>> GetForBookAsync(long bookId, CancellationToken ct)
    {
        var page = await http.GetFromJsonAsync<PagedResult<ReviewDto>>(
            $"/api/reviews?bookId={bookId}&page=1&pageSize={PageSize}", ct);
        return page?.Items ?? [];
    }
}

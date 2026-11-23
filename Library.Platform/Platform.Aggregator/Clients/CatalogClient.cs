using System.Diagnostics;
using System.Net;
using Platform.Aggregator.Models;

namespace Platform.Aggregator.Clients;

public class CatalogClient(HttpClient http, ILogger<CatalogClient> logger)
{
    /// <summary>Картки книг за Id; запити до каталогу — паралельні. Відсутні (404) книги пропускаються.</summary>
    public async Task<IReadOnlyList<BookDto>> GetBooksAsync(IReadOnlyCollection<long> bookIds, CancellationToken ct)
    {
        logger.LogInformation("Calling catalog-api for books {BookIds}", bookIds);
        var sw = Stopwatch.StartNew();

        var books = await Task.WhenAll(bookIds.Select(id => GetBookAsync(id, ct)));

        logger.LogInformation("catalog-api responded for {BooksCount} books in {Elapsed} ms", bookIds.Count, sw.ElapsedMilliseconds);
        return books.Where(b => b is not null).Select(b => b!).ToList();
    }

    private async Task<BookDto?> GetBookAsync(long id, CancellationToken ct)
    {
        using var response = await http.GetAsync($"/api/books/{id}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BookDto>(ct);
    }
}

using System.Diagnostics;
using System.Net;
using Platform.Aggregator.Models;

namespace Platform.Aggregator.Clients;

/// <summary>Типізований клієнт loans-api. CorrelationId додає CorrelationIdHandler (ServiceDefaults).</summary>
public class LoansClient(HttpClient http, ILogger<LoansClient> logger)
{
    public async Task<LoanDto?> GetLoanAsync(long id, CancellationToken ct)
    {
        logger.LogInformation("Calling loans-api for loan {LoanId}", id);
        var sw = Stopwatch.StartNew();

        using var response = await http.GetAsync($"/api/loans/{id}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            logger.LogInformation("loans-api: loan {LoanId} not found ({Elapsed} ms)", id, sw.ElapsedMilliseconds);
            return null;
        }

        response.EnsureSuccessStatusCode();
        var loan = await response.Content.ReadFromJsonAsync<LoanDto>(ct);
        logger.LogInformation("loans-api responded for loan {LoanId} in {Elapsed} ms", id, sw.ElapsedMilliseconds);
        return loan;
    }
}

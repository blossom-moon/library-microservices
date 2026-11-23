using Microsoft.AspNetCore.Mvc;
using Platform.Aggregator.Models;
using Platform.Aggregator.Services;
using Platform.Aggregator.Telemetry;

namespace Platform.Aggregator.Controllers;

[ApiController]
[Route("api/aggregator")]
public class LoanAggregateController(
    LoanAggregationService grpcService, HttpLoanAggregationService httpService, AggregatorMetrics metrics) : ControllerBase
{
    /// <summary>Повна картка видачі: видача + книги + відгуки. Внутрішні виклики — gRPC, відповідь кешується в Redis (30 с).</summary>
    [HttpGet("loans/{id:long}/full")]
    [ProducesResponseType(typeof(FullLoanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public Task<ActionResult<FullLoanDto>> GetFullLoan(long id, CancellationToken ct)
        => RunAsync("grpc", id, grpcService.GetFullLoanAsync, ct);

    /// <summary>Та сама агрегація через HTTP REST (Лаб. 05, без кешу) — лише для порівняння тривалості з gRPC у Traces.</summary>
    [HttpGet("loans/{id:long}/full-http")]
    [ProducesResponseType(typeof(FullLoanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public Task<ActionResult<FullLoanDto>> GetFullLoanOverHttp(long id, CancellationToken ct)
        => RunAsync("http", id, httpService.GetFullLoanAsync, ct);

    /// <summary>Виконує агрегацію й записує метрики для ВСІХ результатів, зокрема невдалих (тривалість, лічильник за результатом).</summary>
    private async Task<ActionResult<FullLoanDto>> RunAsync(
        string transport, long id, Func<long, CancellationToken, Task<FullLoanDto?>> aggregate, CancellationToken ct)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        metrics.Started();
        var result = "error";
        try
        {
            var dto = await aggregate(id, ct);
            if (dto is null)
            {
                result = "not_found";
                return NotFound(new ProblemDetails { Status = 404, Title = "Loan not found", Detail = $"Loan {id} was not found." });
            }

            result = dto.Warnings.Count == 0 ? "ok" : "partial";   // часткова відповідь — окремий результат
            return Ok(dto);
        }
        catch (UpstreamUnavailableException ex)
        {
            result = "unavailable";
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Required service is unavailable", detail: ex.Message);
        }
        finally
        {
            metrics.Finished(transport, result, System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }
}

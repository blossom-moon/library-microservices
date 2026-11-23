using Microsoft.AspNetCore.Mvc;
using Platform.Aggregator.Models;
using Platform.Aggregator.Services;

namespace Platform.Aggregator.Controllers;

[ApiController]
[Route("api/aggregator")]
public class LoanAggregateController(
    LoanAggregationService grpcService, HttpLoanAggregationService httpService) : ControllerBase
{
    /// <summary>Повна картка видачі: видача + книги + відгуки. Внутрішні виклики — gRPC, відповідь кешується в Redis (30 с).</summary>
    [HttpGet("loans/{id:long}/full")]
    [ProducesResponseType(typeof(FullLoanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public Task<ActionResult<FullLoanDto>> GetFullLoan(long id, CancellationToken ct)
        => RunAsync(id, grpcService.GetFullLoanAsync, ct);

    /// <summary>Та сама агрегація через HTTP REST (Лаб. 05, без кешу) — лише для порівняння тривалості з gRPC у Traces.</summary>
    [HttpGet("loans/{id:long}/full-http")]
    [ProducesResponseType(typeof(FullLoanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public Task<ActionResult<FullLoanDto>> GetFullLoanOverHttp(long id, CancellationToken ct)
        => RunAsync(id, httpService.GetFullLoanAsync, ct);

    private async Task<ActionResult<FullLoanDto>> RunAsync(
        long id, Func<long, CancellationToken, Task<FullLoanDto?>> aggregate, CancellationToken ct)
    {
        try
        {
            var result = await aggregate(id, ct);
            return result is null
                ? NotFound(new ProblemDetails { Status = 404, Title = "Loan not found", Detail = $"Loan {id} was not found." })
                : Ok(result);
        }
        catch (UpstreamUnavailableException ex)
        {
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Required service is unavailable", detail: ex.Message);
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using Platform.Aggregator.Models;
using Platform.Aggregator.Services;

namespace Platform.Aggregator.Controllers;

[ApiController]
[Route("api/aggregator")]
public class LoanAggregateController(LoanAggregationService service) : ControllerBase
{
    /// <summary>Повна картка видачі: видача (loans-api) + книги (catalog-api) + відгуки (reviews-api).</summary>
    [HttpGet("loans/{id:long}/full")]
    [ProducesResponseType(typeof(FullLoanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<FullLoanDto>> GetFullLoan(long id, CancellationToken ct)
    {
        try
        {
            var result = await service.GetFullLoanAsync(id, ct);
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

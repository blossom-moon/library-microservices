using FluentValidation;
using Library.Reviews.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace Library.Reviews.WebApi.Middleware;

/// <summary>Єдине місце, де виняток перетворюється на ProblemDetails з правильним кодом.</summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        ProblemDetails problem;

        switch (ex)
        {
            case ValidationException ve:
                var errors = ve.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());
                problem = new ValidationProblemDetails(errors)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Validation failed"
                };
                break;

            case DomainException:
                problem = Make(StatusCodes.Status400BadRequest, "Domain rule violated", ex.Message);
                break;

            case NotFoundException:
                problem = Make(StatusCodes.Status404NotFound, "Not found", ex.Message);
                break;

            case ConflictException:
                problem = Make(StatusCodes.Status409Conflict, "Conflict", ex.Message);
                break;

            case MongoWriteException mwe when mwe.WriteError?.Category == ServerErrorCategory.DuplicateKey:
                problem = Make(StatusCodes.Status409Conflict, "Duplicate", "A document with the same unique key already exists.");
                break;

            case MongoConnectionException or MongoTimeoutException:
                _logger.LogError(ex, "MongoDB is unavailable");
                problem = Make(StatusCodes.Status503ServiceUnavailable, "Database unavailable",
                    "The database is temporarily unavailable. Try again later.");
                break;

            default:
                _logger.LogError(ex, "Unhandled exception");
                problem = Make(StatusCodes.Status500InternalServerError, "Server error", "An unexpected error occurred.");
                break;
        }

        problem.Instance = ctx.Request.Path;
        ctx.Response.StatusCode = problem.Status ?? 500;
        await ctx.Response.WriteAsJsonAsync(problem, ct);
        return true;
    }

    private static ProblemDetails Make(int status, string title, string detail)
        => new() { Status = status, Title = title, Detail = detail };
}

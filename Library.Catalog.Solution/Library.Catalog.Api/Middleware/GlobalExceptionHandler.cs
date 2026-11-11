using FluentValidation;
using Library.Catalog.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Library.Catalog.Api.Middleware;

/// <summary>
/// Єдине місце обробки й логування винятків (кожен виняток логується один раз).
/// EntityNotFoundException → 404; ValidationException → 400; ConflictException / DomainException → 409.
/// Технічні деталі 500-х помилок повертаються лише в Development.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment env)
    {
        _logger = logger;
        _env = env;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            EntityNotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            ValidationException => (StatusCodes.Status400BadRequest, "Validation error"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            DomainException => (StatusCodes.Status409Conflict, "Business rule violation"),
            DbUpdateException { InnerException: Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 } }
                => (StatusCodes.Status409Conflict, "Conflict"),
            _ => (StatusCodes.Status500InternalServerError, "Internal server error"),
        };

        if (status >= 500)
            _logger.LogError(exception, "Unhandled exception on {RequestPath}", httpContext.Request.Path.Value);
        else
            _logger.LogWarning("{ExceptionType} on {RequestPath}: {Message} → {StatusCode}",
                exception.GetType().Name, httpContext.Request.Path.Value, exception.Message, status);

        ProblemDetails problem;
        if (exception is ValidationException validation)
        {
            var errors = validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            problem = new ValidationProblemDetails(errors);
        }
        else
        {
            problem = new ProblemDetails();
        }

        problem.Status = status;
        problem.Title = title;
        problem.Instance = httpContext.Request.Path;
        problem.Detail = status switch
        {
            < 500 when exception is DbUpdateException => "A record with the same unique value already exists.",
            < 500 => exception.Message,
            _ => _env.IsDevelopment() ? exception.Message : "An unexpected error occurred.",
        };
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(problem, problem.GetType(), options: null,
            contentType: "application/problem+json", cancellationToken: ct);

        return true;
    }
}

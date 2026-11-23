using System.Diagnostics;
using Library.Reviews.Application.Common.Telemetry;
using Library.Reviews.Domain.Exceptions;
using MediatR;

namespace Library.Reviews.Application.Common.Behaviors;

/// <summary>
/// Лаб. 07: зовнішній behavior конвейєра — вимірює кожну команду/запит (лічильник за результатом, гістограма
/// тривалості для успіху й помилки). Розташований найзовнішнім, тому враховує й відмови валідації.
/// </summary>
public class MetricsBehavior<TRequest, TResponse>(ReviewMetrics metrics) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private static readonly string Operation = typeof(TRequest).Name;
    private static readonly string Kind = Operation.EndsWith("Query", StringComparison.Ordinal) ? "query" : "command";

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        metrics.Started();
        var started = Stopwatch.GetTimestamp();
        var result = "success";
        try
        {
            return await next();
        }
        catch (Exception ex)
        {
            result = ex switch
            {
                FluentValidation.ValidationException => "validation",
                NotFoundException => "not_found",
                ConflictException => "conflict",
                DomainException => "domain",
                OperationCanceledException => "cancelled",
                _ => "error"
            };
            throw;
        }
        finally
        {
            metrics.Finished(Operation, Kind, result, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }
}

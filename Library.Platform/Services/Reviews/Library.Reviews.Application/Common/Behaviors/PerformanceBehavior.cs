using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Library.Reviews.Application.Common.Behaviors;

/// <summary>Behavior біля обробника: попереджає в лог, якщо запит виконується довше за поріг.</summary>
public class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const int SlowThresholdMs = 500;
    private readonly ILogger<PerformanceBehavior<TRequest, TResponse>> _logger;

    public PerformanceBehavior(ILogger<PerformanceBehavior<TRequest, TResponse>> logger) => _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var response = await next();
        sw.Stop();

        if (sw.ElapsedMilliseconds > SlowThresholdMs)
            _logger.LogWarning("Slow request {RequestName}: {ElapsedMs} ms", typeof(TRequest).Name, sw.ElapsedMilliseconds);

        return response;
    }
}

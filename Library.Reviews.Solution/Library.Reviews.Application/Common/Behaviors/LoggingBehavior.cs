using MediatR;
using Microsoft.Extensions.Logging;

namespace Library.Reviews.Application.Common.Behaviors;

/// <summary>Зовнішній behavior: структуровано логує запит і відповідь. Якщо запит відхилено
/// валідацією, у лозі буде лише «Handling», а обробник не виконується.</summary>
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger) => _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        _logger.LogInformation("Handling {RequestName} {@Request}", requestName, request);
        var response = await next();
        _logger.LogInformation("Handled {RequestName}", requestName);

        return response;
    }
}

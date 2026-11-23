using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace Platform.ServiceDefaults.Correlation;

/// <summary>
/// Наскрізний CorrelationId: береться із заголовка X-Correlation-Id або генерується (GUID),
/// потрапляє в LogContext (усі логи запиту), у заголовок відповіді та в тег поточного span.
/// На початку й у кінці запиту логується межа запиту.
/// </summary>
public class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var generated = false;
        if (!context.Request.Headers.TryGetValue(HeaderName, out var value) || string.IsNullOrEmpty(value))
        {
            value = Guid.NewGuid().ToString();
            context.Request.Headers[HeaderName] = value;   // далі його побачать YARP і HttpClient
            generated = true;
        }

        var correlationId = value.ToString();

        // OnStarting: заголовок гарантовано потрапляє і у відповіді з помилкою (ExceptionHandler чистить заголовки)
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        Activity.Current?.SetTag("correlation.id", correlationId);
        CorrelationContext.Current = correlationId;   // Лаб. 08: подія, опублікована під час запиту, успадкує цей CorrelationId

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            if (generated)
                logger.LogInformation("New CorrelationId generated for {Method} {Path}",
                    context.Request.Method, context.Request.Path.Value);

            var started = Stopwatch.GetTimestamp();
            logger.LogInformation("Request started {Method} {Path}", context.Request.Method, context.Request.Path.Value);

            await next(context);

            logger.LogInformation("Request finished with {StatusCode} in {Elapsed} ms",
                context.Response.StatusCode, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }
}

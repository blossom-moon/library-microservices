using Serilog.Context;

namespace Library.Catalog.Api.Middleware;

/// <summary>
/// Додає до кожного запису логу структуровані властивості CorrelationId, UserId, RequestPath.
/// CorrelationId береться із заголовка X-Correlation-ID або генерується й повертається клієнту.
/// </summary>
public class RequestContextMiddleware
{
    private const string HeaderName = "X-Correlation-ID";
    private readonly RequestDelegate _next;

    public RequestContextMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(correlationId))
            correlationId = Guid.NewGuid().ToString("N");

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("UserId", context.User.Identity?.Name ?? "anonymous"))
        using (LogContext.PushProperty("RequestPath", context.Request.Path.Value))
        {
            await _next(context);
        }
    }
}

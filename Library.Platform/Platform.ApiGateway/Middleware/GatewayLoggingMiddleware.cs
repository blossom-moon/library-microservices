using System.Diagnostics;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;

namespace Platform.ApiGateway.Middleware;

/// <summary>
/// Логування Gateway: метод, шлях, IP клієнта, маршрут і кластер, на який спрямовано запит,
/// статус, тривалість, розмір відповіді та деталі помилок проксі (timeout, connection refused тощо).
/// Усі значення — структуровані властивості, а не конкатенація рядків.
/// </summary>
public class GatewayLoggingMiddleware(RequestDelegate next, ILogger<GatewayLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        var clientIp = context.Connection.RemoteIpAddress?.ToString();

        await next(context);

        var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        var proxy = context.Features.Get<IReverseProxyFeature>();
        var route = proxy?.Route.Config.RouteId ?? "(none)";
        var cluster = proxy?.Cluster?.Config.ClusterId ?? "(none)";

        logger.LogInformation(
            "{Method} {Path} from {ClientIp} -> route {Route}, cluster {Cluster}: {StatusCode} in {Elapsed} ms, {ResponseBytes} bytes",
            context.Request.Method, context.Request.Path.Value, clientIp, route, cluster,
            context.Response.StatusCode, elapsedMs, context.Response.ContentLength);

        var error = context.Features.Get<IForwarderErrorFeature>();
        if (error is not null)
        {
            logger.LogWarning(error.Exception,
                "Proxy error {ProxyError} on route {Route}, cluster {Cluster}: {Message}",
                error.Error, route, cluster, error.Exception?.Message);
        }
    }
}

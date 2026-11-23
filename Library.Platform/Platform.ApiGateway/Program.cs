using Platform.Security.Authentication;
using Platform.ApiGateway.Middleware;
using Platform.ServiceDefaults.Correlation;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();   // Serilog, OpenTelemetry, health checks, service discovery

// Лаб. 09: Gateway перевіряє JWT усіх трьох провайдерів (перший рубіж); сервіси перевіряють токен повторно
builder.AddLibraryAuthentication();
builder.Services.AddAuthorizationBuilder()   // політики для ReverseProxy:Routes:*:AuthorizationPolicy ("anonymous" — вбудована в YARP)
    .AddPolicy("authenticated", p => p.RequireAuthenticatedUser());

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(ctx =>
    {
        // X-User-Id: sub із перевіреного токена (для логів/аудиту downstream; клієнтський заголовок затирається)
        ctx.AddRequestTransform(t =>
        {
            t.ProxyRequest.Headers.Remove("X-User-Id");
            var sub = t.HttpContext.User.FindFirst("sub")?.Value;
            if (!string.IsNullOrEmpty(sub)) t.ProxyRequest.Headers.TryAddWithoutValidation("X-User-Id", sub);
            return ValueTask.CompletedTask;
        });
    })
    .AddServiceDiscoveryDestinationResolver();   // http://loans-api → реальна адреса від Aspire

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();    // ДО MapReverseProxy: генерація/збереження X-Correlation-Id
app.UseMiddleware<GatewayLoggingMiddleware>();   // маршрут, статус, тривалість, помилки проксі
app.UseAuthentication();
app.UseAuthorization();
app.MapReverseProxy();
app.MapDefaultEndpoints();                       // /health і /alive

app.Run();

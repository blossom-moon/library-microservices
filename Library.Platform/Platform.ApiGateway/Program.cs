using Platform.ApiGateway.Middleware;
using Platform.ServiceDefaults.Correlation;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();   // Serilog, OpenTelemetry, health checks, service discovery

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();   // http://loans-api → реальна адреса від Aspire

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();    // ДО MapReverseProxy: генерація/збереження X-Correlation-Id
app.UseMiddleware<GatewayLoggingMiddleware>();   // маршрут, статус, тривалість, помилки проксі
app.MapReverseProxy();
app.MapDefaultEndpoints();                       // /health і /alive

app.Run();

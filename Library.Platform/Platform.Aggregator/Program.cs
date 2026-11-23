using System.Text.Json.Serialization;
using Library.Contracts.Catalog;
using Library.Contracts.Loans;
using Library.Contracts.Reviews;
using Platform.Aggregator.Caching;
using Platform.Aggregator.Clients;
using Platform.Aggregator.Services;
using Platform.ServiceDefaults.Correlation;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();   // Serilog, OpenTelemetry, health checks, service discovery, resilience, CorrelationIdHandler

// Redis (L2) для кешу агрегованих відповідей: ім'я "redis" = ресурс AppHost (ConnectionStrings:redis)
builder.AddRedisDistributedCache("redis");
builder.Services.AddSingleton<AggregatorCacheMetrics>();

// ---- Лаб. 06: типізовані gRPC-клієнти за логічними іменами сервісів (service discovery) ----
// gRPC-клієнти створюються через ту саму фабрику HttpClient, тому ServiceDefaults діють і тут:
// service discovery, resilience, а CorrelationIdHandler додає X-Correlation-Id у метадані виклику.
// Адреса "https://…": сервіси слухають HTTPS (ALPN → HTTP/2), канал тримає одне з'єднання й мультиплексує виклики.
builder.Services.AddGrpcClient<CatalogService.CatalogServiceClient>(o => o.Address = new Uri("https://catalog-api"))
    .AllowDevelopmentCertificate(builder.Environment);
builder.Services.AddGrpcClient<LoansService.LoansServiceClient>(o => o.Address = new Uri("https://loans-api"))
    .AllowDevelopmentCertificate(builder.Environment);
builder.Services.AddGrpcClient<ReviewsService.ReviewsServiceClient>(o => o.Address = new Uri("https://reviews-api"))
    .AllowDevelopmentCertificate(builder.Environment);

// ---- Лаб. 05 (LEGACY, лише для порівняння тривалості): HTTP-клієнти (звичайний HTTP-endpoint, без TLS) ----
builder.Services.AddHttpClient<LoansClient>(c => c.BaseAddress = new("http://loans-api"));
builder.Services.AddHttpClient<CatalogClient>(c => c.BaseAddress = new("http://catalog-api"));
builder.Services.AddHttpClient<ReviewsClient>(c => c.BaseAddress = new("http://reviews-api"));

builder.Services.AddScoped<LoanAggregationService>();       // gRPC + Redis-кеш
builder.Services.AddScoped<HttpLoanAggregationService>();   // HTTP (Лаб. 05)

// JSON: camelCase (за замовчуванням), null-значення не серіалізуються
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);

builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.MapControllers();
app.MapDefaultEndpoints();   // /health і /alive

app.Run();

/// <summary>
/// У Development сертифікат Kestrel — самопідписаний dev-сертифікат; щоб gRPC-виклики не залежали від того,
/// чи довірений він у системі (dotnet dev-certs https --trust), клієнти приймають його лише в Development.
/// </summary>
internal static class GrpcClientBuilderExtensions
{
    public static IHttpClientBuilder AllowDevelopmentCertificate(this IHttpClientBuilder builder, IHostEnvironment environment)
    {
        if (!environment.IsDevelopment()) return builder;

        return builder.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            EnableMultipleHttp2Connections = true,
            SslOptions = { RemoteCertificateValidationCallback = (_, _, _, _) => true }
        });
    }
}

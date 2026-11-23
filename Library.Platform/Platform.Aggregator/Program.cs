using Platform.Security.Authentication;
using Platform.Security.Authorization;
using Platform.Security.ClientCredentials;
using System.Text.Json.Serialization;
using Grpc.Health.V1;
using Library.Contracts.Catalog;
using Library.Contracts.Loans;
using Library.Contracts.Reviews;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Platform.Aggregator.Caching;
using Platform.Aggregator.Clients;
using Platform.Aggregator.HealthChecks;
using Platform.Aggregator.Resilience;
using Platform.Aggregator.Services;
using Platform.Aggregator.Telemetry;
using Platform.ServiceDefaults.Correlation;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();   // Serilog, OpenTelemetry (+Polly meter), health checks, service discovery, стандартний resilience-обробник для ВСІХ клієнтів

// Лаб. 09: вхідні токени (Local/IdentityServer/Keycloak) та вихідний токен Client Credentials до downstream-сервісів
builder.AddLibraryAuthentication();
builder.Services.AddLibraryAuthorization();
builder.Services.AddClientCredentials(builder.Configuration);

// Redis (L2) для кешу агрегованих відповідей: ім'я "redis" = ресурс AppHost (ConnectionStrings:redis).
// Вбудовану перевірку Aspire вимкнено: Redis — необов'язкова залежність, тому власна перевірка нижче дає Degraded, а не Unhealthy.
builder.AddRedisDistributedCache("redis", settings => settings.DisableHealthChecks = true);
builder.Services.AddSingleton<AggregatorCacheMetrics>();
builder.Services.AddSingleton<AggregatorMetrics>();   // Лаб. 07: бізнес-метрики (Singleton — живуть увесь час роботи застосунку)

// ---- gRPC-клієнти за логічними іменами сервісів (service discovery), Лаб. 06 ----
// Resilience (Лаб. 07): стандартний обробник уже діє з ServiceDefaults; для кожного клієнта його ЗАМІНЕНО власним
// (RemoveAllResilienceHandlers + AddStandardResilienceHandler у UseResiliencePolicy) — обробники не нашаровуються.
// Повтори за gRPC-статусами (Unavailable) — вбудована retry-політика каналу (UseGrpcRetries).
builder.Services.AddGrpcClient<CatalogService.CatalogServiceClient>(o => o.Address = new Uri("https://catalog-api"))
    .AllowDevelopmentCertificate(builder.Environment)
    .UseResiliencePolicy(ResiliencePolicies.Catalog)     // критичний
    .AddHttpMessageHandler<ClientCredentialsTokenHandler>()   // Bearer-токен Client Credentials
    .UseGrpcRetries(ResiliencePolicies.Catalog);
builder.Services.AddGrpcClient<LoansService.LoansServiceClient>(o => o.Address = new Uri("https://loans-api"))
    .AllowDevelopmentCertificate(builder.Environment)
    .UseResiliencePolicy(ResiliencePolicies.Loans)       // критичний
    .AddHttpMessageHandler<ClientCredentialsTokenHandler>()   // Bearer-токен Client Credentials
    .UseGrpcRetries(ResiliencePolicies.Loans);
builder.Services.AddGrpcClient<ReviewsService.ReviewsServiceClient>(o => o.Address = new Uri("https://reviews-api"))
    .AllowDevelopmentCertificate(builder.Environment)
    .UseResiliencePolicy(ResiliencePolicies.Reviews)     // некритичний
    .AddHttpMessageHandler<ClientCredentialsTokenHandler>()   // Bearer-токен Client Credentials
    .UseGrpcRetries(ResiliencePolicies.Reviews);

// ---- gRPC Health Checking Protocol: клієнти для перевірки залежностей (без повторів і breaker — перевірка має показувати реальний стан) ----
foreach (var (clientName, address) in new[]
{
    ("loans-health", "https://loans-api"), ("catalog-health", "https://catalog-api"), ("reviews-health", "https://reviews-api")
})
{
    builder.Services.AddGrpcClient<Health.HealthClient>(clientName, o => o.Address = new Uri(address))
        .AllowDevelopmentCertificate(builder.Environment)
        .RemoveAllResilienceHandlers();
}

// ---- Лаб. 05 (LEGACY, лише для порівняння тривалості): HTTP-клієнти (звичайний HTTP-endpoint, без TLS) ----
builder.Services.AddHttpClient<LoansClient>(c => c.BaseAddress = new("http://loans-api"))
    .UseResiliencePolicy(ResiliencePolicies.Loans)
    .AddHttpMessageHandler<ClientCredentialsTokenHandler>();
builder.Services.AddHttpClient<CatalogClient>(c => c.BaseAddress = new("http://catalog-api"))
    .UseResiliencePolicy(ResiliencePolicies.Catalog)
    .AddHttpMessageHandler<ClientCredentialsTokenHandler>();
builder.Services.AddHttpClient<ReviewsClient>(c => c.BaseAddress = new("http://reviews-api"))
    .UseResiliencePolicy(ResiliencePolicies.Reviews)
    .AddHttpMessageHandler<ClientCredentialsTokenHandler>();

builder.Services.AddScoped<LoanAggregationService>();       // gRPC + Redis-кеш
builder.Services.AddScoped<HttpLoanAggregationService>();   // HTTP (Лаб. 05)

// ---- Health checks (readiness = теги "ready"): залежності Aggregator ----
// Критичні сервіси → Unhealthy, некритичний (відгуки) і кеш → Degraded: агрегатор може працювати й без них.
builder.Services.AddHealthChecks()
    .AddTypeActivatedCheck<GrpcDependencyHealthCheck>("loans-api", HealthStatus.Unhealthy,
        ["grpc", "downstream", "ready"], "loans-health", "loans-api")
    .AddTypeActivatedCheck<GrpcDependencyHealthCheck>("catalog-api", HealthStatus.Unhealthy,
        ["grpc", "downstream", "ready"], "catalog-health", "catalog-api")
    .AddTypeActivatedCheck<GrpcDependencyHealthCheck>("reviews-api", HealthStatus.Degraded,
        ["grpc", "downstream", "ready"], "reviews-health", "reviews-api")
    .AddRedis(
        redisConnectionString: builder.Configuration.GetConnectionString("redis")!,
        name: "redis-cache",
        failureStatus: HealthStatus.Degraded,        // кеш необов'язковий: його відсутність не робить сервіс Unhealthy
        tags: ["cache", "redis", "ready"],
        timeout: TimeSpan.FromSeconds(3));

// JSON: camelCase (за замовчуванням), null-значення не серіалізуються
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);

builder.Services.AddProblemDetails();

var app = builder.Build();

// Колбеки Polly (повтори, circuit breaker, таймаути) логуються з категорією "Polly"
ResilienceExtensions.LoggerFactory = app.Services.GetRequiredService<ILoggerFactory>();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapDefaultEndpoints();   // /health/live, /health/ready (+ /health і /alive у Development)

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

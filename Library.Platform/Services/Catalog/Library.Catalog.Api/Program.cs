using Platform.Security;
using Platform.Security.Authentication;
using Platform.Security.Authorization;
using FluentValidation;
using Library.Catalog.Api.GrpcServices;
using Library.Catalog.Api.HealthChecks;
using Library.Catalog.Api.Middleware;
using Library.Catalog.Bll;
using Library.Catalog.Bll.Caching;
using Library.Catalog.Bll.Interfaces;
using Library.Catalog.Bll.Services;
using Library.Catalog.Bll.Telemetry;
using Library.Catalog.Bll.Validators;
using Library.Catalog.Dal;
using Library.Catalog.Dal.Interfaces;
using Library.Catalog.Dal.Repositories;
using Library.Catalog.Dal.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Platform.Messaging;
using Platform.ServiceDefaults.Correlation;
using Platform.ServiceDefaults.GrpcSupport;

var builder = WebApplication.CreateBuilder(args);

// ---- .NET Aspire: Serilog, OpenTelemetry, health checks, service discovery (Лаб. 05) ----
builder.AddServiceDefaults();

// ---- EF Core + Unit of Work ----
var connectionString = builder.Configuration.GetConnectionString("CatalogDb")
    ?? throw new InvalidOperationException("Connection string 'CatalogDb' not found.");

builder.Services.AddDbContext<CatalogDbContext>(o => o.UseSqlServer(connectionString));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// ---- Лаб. 06: двохрівневе кешування ----
// L1 — IMemoryCache з обмеженням розміру (Size у KiB; SizeLimit ≈ 10 MiB) та витісненням за пріоритетом/терміном
builder.Services.AddMemoryCache(o =>
{
    o.SizeLimit = 10_240;                                   // сумарний Size усіх записів
    o.CompactionPercentage = 0.25;                          // при переповненні витісняється 25 % записів
    o.ExpirationScanFrequency = TimeSpan.FromMinutes(1);
    o.TrackStatistics = true;                               // для метрик cache.l1.size / cache.l1.entries
});
// L2 — Redis через Aspire (ім'я "redis" = ім'я ресурсу в AppHost; рядок підключення ConnectionStrings:redis)
// Вбудовану перевірку Aspire вимкнено: нижче власна перевірка Redis зі статусом Degraded (кеш необов'язковий)
builder.AddRedisDistributedCache("redis", settings => settings.DisableHealthChecks = true);
builder.Services.AddSingleton<CacheMetrics>();
builder.Services.AddSingleton<ITwoLevelCache, TwoLevelCache>();
builder.Services.AddSingleton<CatalogMetrics>();   // Лаб. 07: бізнес-метрики (Singleton)

// ---- Лаб. 07: health checks ----
// SQL Server — критична залежність (Unhealthy); Redis — необов'язкова (Degraded: сервіс працює, але без L2-кешу)
builder.Services.AddHealthChecks()
    .AddCheck<CacheHealthCheck>("cache", tags: ["cache"])
    .AddSqlServer(connectionString, name: "catalog-db", failureStatus: HealthStatus.Unhealthy,
        tags: ["database", "sql", "ready"], timeout: TimeSpan.FromSeconds(5))
    .AddRedis(builder.Configuration.GetConnectionString("redis")!, name: "redis-cache",
        failureStatus: HealthStatus.Degraded, tags: ["cache", "redis", "ready"], timeout: TimeSpan.FromSeconds(3));
builder.Services.AddGrpcReadinessHealth();

// ---- Лаб. 08: RabbitMQ — IConnection (Aspire), publisher, оголошення топології при старті ----
builder.AddMessaging();

// ---- Лаб. 06: gRPC (серверний interceptor логує метод, peer, статус і тривалість) ----
builder.Services.AddGrpc(o => o.Interceptors.Add<GrpcLoggingInterceptor>());

// ---- BLL ----
builder.Services.AddScoped<IBookService, BookService>();
builder.Services.AddScoped<IAuthorService, AuthorService>();

// ---- AutoMapper (Community-ліцензія: ключ береться з конфігурації, якщо заданий) ----
var autoMapperKey = builder.Configuration["AutoMapper:LicenseKey"];
builder.Services.AddAutoMapper(cfg =>
{
    if (!string.IsNullOrWhiteSpace(autoMapperKey)) cfg.LicenseKey = autoMapperKey;
    cfg.AddProfile<MappingProfile>();
});

// ---- FluentValidation: реєстрація валідаторів; викликаються явно через IValidator<T> ----
builder.Services.AddValidatorsFromAssemblyContaining<CreateBookDtoValidator>();

// ---- API, Swagger, ProblemDetails ----
// Обов'язковість полів задається явно ([Required] на DTO), а не неявно через nullable-анотації
builder.Services.AddControllers(o => o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Лаб. 09: JWT (Local/IdentityServer/Keycloak) + політики дозволів
builder.AddLibraryAuthentication(ApiResourceNames.Catalog);
builder.Services.AddLibraryAuthorization();

var app = builder.Build();

// ---- Міграції та ідемпотентний сідінг (вимикається в Production або Seeding:Enabled=false) ----
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    await db.Database.MigrateAsync();

    if (!app.Environment.IsProduction() && app.Configuration.GetValue("Seeding:Enabled", true))
        await DbInitializer.SeedAsync(db);
}

app.UseMiddleware<CorrelationIdMiddleware>();    // наскрізний X-Correlation-Id (ServiceDefaults)
app.UseExceptionHandler();                       // глобальна обробка → ProblemDetails

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGrpcService<CatalogGrpcService>();       // gRPC-контракт Protos/catalog.proto
app.MapGrpcReadinessHealth();                   // grpc.health.v1.Health (Лаб. 07)
app.MapDefaultEndpoints();                       // /health/live, /health/ready

app.Run();

using Platform.Security;
using Platform.Security.Authentication;
using Platform.Security.Authorization;
using Library.Reviews.Application;
using Library.Reviews.Application.Reviews;
using Library.Reviews.Infrastructure;
using Library.Reviews.WebApi.GrpcServices;
using Library.Reviews.WebApi.Middleware;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Driver;
using Library.Reviews.Infrastructure.Messaging;
using Platform.Messaging;
using Platform.Messaging.Events;
using Platform.Messaging.Topology;
using Platform.ServiceDefaults.Correlation;
using Platform.ServiceDefaults.GrpcSupport;

var builder = WebApplication.CreateBuilder(args);

// .NET Aspire: Serilog, OpenTelemetry, health checks, service discovery, resilience (Лаб. 05)
builder.AddServiceDefaults();

// Рядок підключення від Aspire (ConnectionStrings:ReviewsDb) має пріоритет над секцією MongoDb з appsettings.json;
// без Aspire сервіс, як і раніше, працює з MongoDb:ConnectionString (user-secrets / Atlas).
var reviewsDb = builder.Configuration.GetConnectionString("ReviewsDb");
if (!string.IsNullOrWhiteSpace(reviewsDb))
{
    builder.Configuration["MongoDb:ConnectionString"] = reviewsDb;
    builder.Configuration["MongoDb:DatabaseName"] = MongoUrl.Create(reviewsDb).DatabaseName ?? "ReviewsDb";
}

var licenseKey = builder.Configuration["LuckyPenny:LicenseKey"];

builder.Services.AddApplication(licenseKey);
builder.Services.AddInfrastructure(builder.Configuration);

// Лаб. 07: health check MongoDB (критична залежність → Unhealthy; ping із таймаутом 5 с); gRPC Health Checking Protocol
builder.Services.AddHealthChecks()
    .AddMongoDb(sp => sp.GetRequiredService<IMongoClient>(), name: "reviews-db",
        failureStatus: HealthStatus.Unhealthy, tags: ["database", "mongodb", "ready"], timeout: TimeSpan.FromSeconds(5));
builder.Services.AddGrpcReadinessHealth();

// Лаб. 08: RabbitMQ (IConnection через Aspire, топологія при старті) і споживач черги reviews.catalog-sync
builder.AddMessaging();
builder.Services.AddEventConsumer(MessagingTopology.ReviewsQueue, registry => registry
    .Handle<BookRenamedEvent, BookRenamedHandler>());

builder.Services.AddAutoMapper(cfg =>
{
    if (!string.IsNullOrWhiteSpace(licenseKey)) cfg.LicenseKey = licenseKey;
}, typeof(ReviewProfile).Assembly, typeof(Program).Assembly);

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(o => o.SuppressMapClientErrors = false);
builder.Services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(o =>
    o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);

// Лаб. 06: gRPC (серверний interceptor логує метод, peer, статус і тривалість)
builder.Services.AddGrpc(o => o.Interceptors.Add<GrpcLoggingInterceptor>());

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Лаб. 09: JWT (Local/IdentityServer/Keycloak) + політики дозволів
builder.AddLibraryAuthentication(ApiResourceNames.Reviews);
builder.Services.AddLibraryAuthorization();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();   // наскрізний X-Correlation-Id (ServiceDefaults)
app.UseExceptionHandler();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGrpcService<ReviewsGrpcService>();   // контракт Protos/reviews.proto
app.MapGrpcReadinessHealth();               // grpc.health.v1.Health (Лаб. 07)
app.MapDefaultEndpoints();                  // /health/live, /health/ready

app.Run();

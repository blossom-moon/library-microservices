using Library.Reviews.Application;
using Library.Reviews.Application.Reviews;
using Library.Reviews.Infrastructure;
using Library.Reviews.WebApi.GrpcServices;
using Library.Reviews.WebApi.Middleware;
using MongoDB.Driver;
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

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();   // наскрізний X-Correlation-Id (ServiceDefaults)
app.UseExceptionHandler();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapGrpcService<ReviewsGrpcService>();   // контракт Protos/reviews.proto
app.MapDefaultEndpoints();   // /health (зокрема перевірка MongoDB з AddInfrastructure) і /alive

app.Run();

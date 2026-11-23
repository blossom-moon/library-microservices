using System.Text.Json.Serialization;
using Platform.Aggregator.Clients;
using Platform.Aggregator.Services;
using Platform.ServiceDefaults.Correlation;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();   // Serilog, OpenTelemetry, health checks, service discovery, CorrelationIdHandler

// Типізовані клієнти за логічними іменами сервісів (service discovery);
// "https+http" — HTTPS, якщо доступний, інакше HTTP. CorrelationId додається автоматично.
builder.Services.AddHttpClient<LoansClient>(c => c.BaseAddress = new("https+http://loans-api"));
builder.Services.AddHttpClient<CatalogClient>(c => c.BaseAddress = new("https+http://catalog-api"));
builder.Services.AddHttpClient<ReviewsClient>(c => c.BaseAddress = new("https+http://reviews-api"));

builder.Services.AddScoped<LoanAggregationService>();

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

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Platform.ServiceDefaults.Correlation;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Спільні налаштування всіх сервісів платформи: структуроване логування (Serilog),
/// розподілене трасування й метрики (OpenTelemetry), health checks, service discovery,
/// resilience та наскрізний CorrelationId для вихідних HTTP-викликів.
/// Підключається одним викликом builder.AddServiceDefaults().
/// </summary>
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.AddSerilogLogging();        // Частина 4.1
        builder.ConfigureOpenTelemetry();   // Частина 4.3
        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddTransient<CorrelationIdHandler>();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Помірні повтори: недоступний сервіс не повинен гальмувати агрегатор на десятки секунд
            http.AddStandardResilienceHandler(o =>
            {
                o.Retry.MaxRetryAttempts = 2;
                o.Retry.Delay = TimeSpan.FromMilliseconds(300);
            });
            http.AddServiceDiscovery();                           // логічні імена: http://orders-api
            http.AddHttpMessageHandler<CorrelationIdHandler>();   // Частина 4.2
        });

        return builder;
    }

    /// <summary>Serilog: кожен запис — структурований об'єкт; дублюється в OpenTelemetry (Structured logs у Dashboard).</summary>
    public static TBuilder AddSerilogLogging<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? builder.Environment.ApplicationName;
        var isDevelopment = builder.Environment.IsDevelopment();

        // Стандартні провайдери (Console тощо) прибираємо, щоб не було дублювання з Serilog;
        // провайдер OpenTelemetry додається в ConfigureOpenTelemetry.
        builder.Logging.ClearProviders();

        builder.Services.AddSerilog((services, cfg) => cfg
                .MinimumLevel.Is(isDevelopment ? LogEventLevel.Debug : LogEventLevel.Information)
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .Enrich.FromLogContext()                                  // сюди потрапляє CorrelationId
                .Enrich.WithProperty("ServiceName", serviceName)
                .Enrich.WithEnvironmentName()
                .Enrich.WithMachineName()
                .Enrich.WithThreadId()
                .WriteTo.Console(new CompactJsonFormatter()),             // JSON (TraceId/SpanId — @tr/@sp)
            writeToProviders: true);                                      // → OpenTelemetry → Structured logs у Dashboard

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? builder.Environment.ApplicationName;
        var isDevelopment = builder.Environment.IsDevelopment();

        builder.Logging.AddOpenTelemetry(o =>
        {
            o.IncludeFormattedMessage = true;
            o.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddAttributes(new KeyValuePair<string, object>[]
            {
                new("ServiceName", serviceName),
                new("Environment", builder.Environment.EnvironmentName)
            }))
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                // Лаб. 06–07: власні метрики. Назва Meter відповідає сервісу (Loans.Api, Catalog.Api, Reviews.Api, Aggregator.Api);
                // "Library.*"/"Platform.*" — метрики кешу (Library.Catalog.Cache, Platform.Aggregator.Cache);
                // "Polly" — телеметрія resilience (resilience.polly.strategy.events, ...attempt.duration, ...pipeline.duration)
                .AddMeter("Loans.Api", "Catalog.Api", "Reviews.Api", "Aggregator.Api")
                .AddMeter("Library.*", "Platform.*")
                .AddMeter("Polly"))
            .WithTracing(t => t
                // sampling: 100 % у Development, 10 % у Production
                .SetSampler(isDevelopment
                    ? new AlwaysOnSampler()
                    : new ParentBasedSampler(new TraceIdRatioBasedSampler(0.1)))
                .AddSource(builder.Environment.ApplicationName)
                .AddSource("Platform.Messaging")                                // Лаб. 08: publish/consume через RabbitMQ
                .AddSource("Library.*", "Platform.*")                           // власні spans бізнес-операцій
                .AddSource("MongoDB.Driver.Core.Extensions.DiagnosticSources")  // команди MongoDB
                .AddAspNetCoreInstrumentation(o => o.Filter = ctx =>
                    !ctx.Request.Path.StartsWithSegments(HealthEndpointPath) &&
                    !ctx.Request.Path.StartsWithSegments(AlivenessEndpointPath))
                .AddHttpClientInstrumentation()
                .AddSqlClientInstrumentation());                                // Dapper і EF Core

        // Адресу OTLP-колектора (Aspire Dashboard) задає AppHost
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        // "self" — лише факт, що процес відповідає: без зовнішніх залежностей, тому тег "live" (liveness).
        // Тег "ready" теж потрібен, щоб readiness-endpoint не був порожнім у сервісів без залежностей (Gateway).
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy("Process is running."), ["live", "ready"]);

        return builder;
    }

    /// <summary>
    /// Endpoints стану (Лаб. 07, частина 3.5):
    ///   /health/live  — liveness: лише тег "live", без зовнішніх залежностей, надшвидко (збій → оркестратор перезапускає процес);
    ///   /health/ready — readiness: перевірки з тегом "ready" (БД, Redis, залежності) з деталями у JSON
    ///                   (збій → оркестратор тимчасово виводить екземпляр із балансування);
    ///   /health, /alive — повний набір та «self» (сумісність з Лаб. 05–06), лише в Development.
    /// live/ready доступні в усіх середовищах — їх використовують AppHost/Kubernetes; деталі помилок у тілі — лише в Development.
    /// </summary>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live"),
            ResponseWriter = WriteJsonResponse
        });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = WriteJsonResponse        // деталі кожної перевірки у JSON
            // ResultStatusCodes за замовчуванням: Healthy 200, Degraded 200 (сервіс працює, але без необов'язкової залежності), Unhealthy 503
        });

        if (app.Environment.IsDevelopment())
        {
            app.MapHealthChecks(HealthEndpointPath, new HealthCheckOptions { ResponseWriter = WriteJsonResponse });
            app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live")
            });
        }

        return app;
    }

    /// <summary>JSON-відповідь health check: загальний статус і деталі кожної перевірки (ім'я, статус, тривалість, теги, дані).</summary>
    private static Task WriteJsonResponse(HttpContext context, HealthReport report)
    {
        // Тексти винятків можуть містити адреси та рядки підключення — показуємо їх лише в Development
        var showErrors = context.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment();

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                durationMs = Math.Round(e.Value.Duration.TotalMilliseconds, 1),
                description = e.Value.Description,
                tags = e.Value.Tags,
                data = e.Value.Data.ToDictionary(d => d.Key, d => d.Value),
                error = showErrors ? e.Value.Exception?.Message : null
            })
        };

        context.Response.ContentType = "application/json; charset=utf-8";
        return context.Response.WriteAsJsonAsync(payload);
    }
}

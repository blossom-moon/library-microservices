using Microsoft.AspNetCore.Builder;
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
                .AddRuntimeInstrumentation())
            .WithTracing(t => t
                // sampling: 100 % у Development, 10 % у Production
                .SetSampler(isDevelopment
                    ? new AlwaysOnSampler()
                    : new ParentBasedSampler(new TraceIdRatioBasedSampler(0.1)))
                .AddSource(builder.Environment.ApplicationName)
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
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    /// <summary>/health — усі перевірки (готовність), /alive — лише "self" (живість). Лише в Development.</summary>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapHealthChecks(HealthEndpointPath);
            app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live")
            });
        }

        return app;
    }
}

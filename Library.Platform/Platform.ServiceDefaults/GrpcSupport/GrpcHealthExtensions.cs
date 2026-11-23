using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Platform.ServiceDefaults.GrpcSupport;

/// <summary>
/// Стандартний gRPC Health Checking Protocol (grpc.health.v1.Health, Лаб. 07, частина 3.4): сервіс відповідає
/// Serving / NotServing на основі тих самих health checks ASP.NET Core — лише перевірок з тегом "ready".
/// Aggregator викликає Health.Check і так дізнається, чи готова залежність приймати виклики.
/// </summary>
public static class GrpcHealthExtensions
{
    public static IServiceCollection AddGrpcReadinessHealth(this IServiceCollection services)
    {
        // Відповідь береться з опублікованого звіту health checks: службове значення "" = стан сервера загалом
        services.AddGrpcHealthChecks(o => o.Services.MapService("", registration => registration.Tags.Contains("ready")));

        // Звіт оновлюється кожні 10 с (за замовчуванням — 30 с), щоб зміни швидко потрапляли в gRPC-відповідь
        services.Configure<HealthCheckPublisherOptions>(o =>
        {
            o.Delay = TimeSpan.FromSeconds(2);
            o.Period = TimeSpan.FromSeconds(10);
        });

        return services;
    }

    public static WebApplication MapGrpcReadinessHealth(this WebApplication app)
    {
        app.MapGrpcHealthChecksService();
        return app;
    }
}

using FluentValidation;
using Library.Reviews.Application.Common.Behaviors;
using Library.Reviews.Application.Common.Telemetry;
using Microsoft.Extensions.DependencyInjection;

namespace Library.Reviews.Application;

public static class DependencyInjection
{
    /// <param name="licenseKey">Community-ключ Lucky Penny Software (MediatR/AutoMapper); необов'язковий.</param>
    public static IServiceCollection AddApplication(this IServiceCollection services, string? licenseKey = null)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            if (!string.IsNullOrWhiteSpace(licenseKey)) cfg.LicenseKey = licenseKey;

            cfg.RegisterServicesFromAssembly(assembly);               // обробники команд і запитів

            // Порядок AddOpenBehavior = порядок виконання (зовнішній → внутрішній)
            cfg.AddOpenBehavior(typeof(MetricsBehavior<,>));          // 1 — зовнішній (Лаб. 07: бізнес-метрики)
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));          // 2
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));       // 3
            cfg.AddOpenBehavior(typeof(PerformanceBehavior<,>));      // 4 — біля обробника
        });

        services.AddSingleton<ReviewMetrics>();   // Лаб. 07: Singleton — один Meter на застосунок

        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}

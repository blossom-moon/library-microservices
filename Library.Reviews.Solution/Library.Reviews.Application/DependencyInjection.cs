using FluentValidation;
using Library.Reviews.Application.Common.Behaviors;
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
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));          // 1 — зовнішній
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));       // 2
            cfg.AddOpenBehavior(typeof(PerformanceBehavior<,>));      // 3 — біля обробника
        });

        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}

using Library.Reviews.Application.Common.Interfaces;
using Library.Reviews.Domain.Interfaces;
using Library.Reviews.Infrastructure.Indexes;
using Library.Reviews.Infrastructure.Persistence;
using Library.Reviews.Infrastructure.Queries;
using Library.Reviews.Infrastructure.Repositories;
using Library.Reviews.Infrastructure.Seeding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace Library.Reviews.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MongoDbOptions>()
            .Bind(configuration.GetSection(MongoDbOptions.SectionName))
            .Validate(o => o.ConnectionString.StartsWith("mongodb://") || o.ConnectionString.StartsWith("mongodb+srv://"),
                "MongoDb:ConnectionString is not configured. Set it with: dotnet user-secrets set \"MongoDb:ConnectionString\" \"mongodb+srv://...\"")
            .Validate(o => !string.IsNullOrWhiteSpace(o.DatabaseName), "MongoDb:DatabaseName is not configured.")
            .ValidateOnStart();

        services.AddSingleton<MongoDbContext>();
        // Лаб. 07: той самий клієнт (один пул з'єднань) використовує health check MongoDB у WebApi
        services.AddSingleton<IMongoClient>(sp => sp.GetRequiredService<MongoDbContext>().Client);

        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<IReviewQueries, ReviewQueries>();

        services.AddSingleton<IDataSeeder, ReviewSeeder>();
        services.AddSingleton<IDataSeeder, DiscussionSeeder>();

        // Порядок важливий: спочатку індекси, потім seed
        services.AddHostedService<IndexCreationService>();
        services.AddHostedService<DataSeedingService>();

        // Лаб. 07: health check MongoDB реєструється в WebApi (AddMongoDb з іменем, тегами й таймаутом)

        return services;
    }
}

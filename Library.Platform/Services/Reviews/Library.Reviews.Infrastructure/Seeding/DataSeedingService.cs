using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Library.Reviews.Infrastructure.Seeding;

/// <summary>Запускає всі IDataSeeder при старті, якщо Seeding:Enabled = true.</summary>
public class DataSeedingService : IHostedService
{
    private readonly IEnumerable<IDataSeeder> _seeders;
    private readonly IConfiguration _config;
    private readonly ILogger<DataSeedingService> _logger;

    public DataSeedingService(IEnumerable<IDataSeeder> seeders, IConfiguration config, ILogger<DataSeedingService> logger)
    {
        _seeders = seeders;
        _config = config;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        if (!_config.GetValue("Seeding:Enabled", true)) return;

        try
        {
            foreach (var seeder in _seeders)
                await seeder.SeedAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Seeding failed (is MongoDB reachable?).");
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}

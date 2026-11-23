using System.Reflection;
using Microsoft.Data.SqlClient;

namespace Library.Loans.Api.Database;

/// <summary>
/// Лаб. 10: у Kubernetes ніхто не виконує <c>WithCreationScript</c> з AppHost (aspirate створює лише контейнер SQL Server),
/// тому за <c>Database:InitializeOnStartup=true</c> сервіс сам виконує вбудований скрипт loans-init.sql
/// (той самий файл, що й в AppHost). Скрипт ідемпотентний: перевірки IF DB_ID / OBJECT_ID / COL_LENGTH.
/// SQL Server у новому поді може стартувати довше за сервіс — підключення повторюється.
/// </summary>
public static class LoansDatabaseInitializer
{
    private const string ResourceName = "loans-init.sql";

    public static async Task RunAsync(string connectionString, ILogger logger, CancellationToken ct = default)
    {
        var master = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" }.ConnectionString;

        await using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' not found.");
        using var reader = new StreamReader(stream);
        var script = await reader.ReadToEndAsync(ct);

        const int attempts = 30;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var connection = new SqlConnection(master);
                await connection.OpenAsync(ct);
                await using var command = connection.CreateCommand();
                command.CommandText = script;           // один пакет, як у WithCreationScript
                command.CommandTimeout = 120;
                await command.ExecuteNonQueryAsync(ct);
                logger.LogInformation("Loans database initialized (attempt {Attempt})", attempt);
                return;
            }
            catch (SqlException ex) when (attempt < attempts)
            {
                logger.LogWarning("SQL Server is not ready ({Message}); retry {Attempt}/{Attempts} in 4 s", ex.Message, attempt, attempts);
                await Task.Delay(TimeSpan.FromSeconds(4), ct);
            }
        }
    }
}

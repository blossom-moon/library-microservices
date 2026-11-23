using Dapper;
using Library.Loans.Dal.Interfaces;
using Microsoft.Data.SqlClient;

namespace Library.Loans.Dal.Repositories;

public class ProcessedEventRepository : RepositoryBase, IProcessedEventRepository
{
    public ProcessedEventRepository(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
        : base(connection, currentTransaction) { }

    public async Task<bool> TryAddAsync(Guid eventId, string eventType, CancellationToken ct)
    {
        await EnsureOpenAsync(ct);

        // Атомарно: вставка лише якщо EventId ще немає. UPDLOCK + HOLDLOCK блокують паралельну обробку того самого
        // EventId до кінця транзакції; PRIMARY KEY — остаточний захист (помилки 2627/2601 теж означають «дублікат»).
        const string sql = @"
            INSERT INTO dbo.ProcessedEvents (EventId, EventType)
            SELECT @EventId, @EventType
            WHERE NOT EXISTS (SELECT 1 FROM dbo.ProcessedEvents WITH (UPDLOCK, HOLDLOCK) WHERE EventId = @EventId);";

        try
        {
            var command = new CommandDefinition(sql, new { EventId = eventId, EventType = eventType }, Transaction, cancellationToken: ct);
            return await Connection.ExecuteAsync(command) == 1;
        }
        catch (SqlException ex) when (ex.Number is 2627 or 2601)
        {
            return false;
        }
    }
}

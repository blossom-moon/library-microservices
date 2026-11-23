using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Library.Loans.Domain;
using Library.Loans.Dal.Interfaces;

namespace Library.Loans.Dal.Repositories;

/// <summary>
/// Dapper-репозиторій. GetByIdAsync/GetAllAsync/AddAsync/DeleteAsync успадковані
/// з BaseDapperRepository&lt;Book&gt; — тут лишається тільки специфічний для
/// книг запит.
/// </summary>
public class BookRepository : BaseDapperRepository<Book>, IBookRepository
{
    public BookRepository(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
        : base(connection, currentTransaction, "dbo.Books", "BookId")
    {
    }

    public Task<bool> UpdatePriceAsync(long bookId, decimal price, DateTime occurredAt, CancellationToken ct) =>
        UpdateIfNewerAsync(
            @"UPDATE dbo.Books SET Price = @Value, PriceChangedAt = @At, SyncedAt = SYSUTCDATETIME()
              WHERE BookId = @BookId AND (PriceChangedAt IS NULL OR PriceChangedAt < @At)",
            bookId, price, DbType.Decimal, occurredAt, ct);

    public Task<bool> UpdateTitleAsync(long bookId, string title, DateTime occurredAt, CancellationToken ct) =>
        UpdateIfNewerAsync(
            @"UPDATE dbo.Books SET Title = @Value, TitleChangedAt = @At, SyncedAt = SYSUTCDATETIME()
              WHERE BookId = @BookId AND (TitleChangedAt IS NULL OR TitleChangedAt < @At)",
            bookId, title, DbType.String, occurredAt, ct);

    // Оновлення лише якщо подія новіша за останню застосовану: запізніла (out-of-order) подія не перезапише новіші дані
    private async Task<bool> UpdateIfNewerAsync(string sql, long bookId, object value, DbType valueType, DateTime occurredAt, CancellationToken ct)
    {
        await EnsureOpenAsync(ct);

        var parameters = new DynamicParameters();
        parameters.Add("BookId", bookId);
        parameters.Add("Value", value, valueType);
        parameters.Add("At", occurredAt, DbType.DateTime2);

        var command = new CommandDefinition(sql, parameters, Transaction, cancellationToken: ct);
        return await Connection.ExecuteAsync(command) > 0;
    }

    public async Task<bool> AreAllAvailableAsync(IEnumerable<long> bookIds, CancellationToken ct)
    {
        await EnsureOpenAsync(ct);

        var ids = bookIds.ToList();
        const string sql = @"SELECT COUNT(*) FROM dbo.Books
                              WHERE BookId IN @BookIds AND IsAvailable = 1";

        var command = new CommandDefinition(sql, new { BookIds = ids }, Transaction, cancellationToken: ct);
        var availableCount = await Connection.ExecuteScalarAsync<int>(command);
        return availableCount == ids.Count;
    }
}

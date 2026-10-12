using System.Data;
using Microsoft.Data.SqlClient;
using Library.Loans.Domain;
using Library.Loans.Dal.Interfaces;

namespace Library.Loans.Dal.Repositories;

/// <summary>
/// Єдиний репозиторій на чистому ADO.NET (без Dapper) — демонструє роботу
/// з SqlCommand/SqlDataReader напряму. Свідомо НЕ реалізує IGenericRepository&lt;T&gt;:
/// мета саме показати ручне керування підключенням і мапінгом, а не ховати це
/// за узагальненим контрактом (див. Лаб.02, крок B.2).
/// </summary>
public class ReaderRepository : RepositoryBase, IReaderRepository
{
    public ReaderRepository(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
        : base(connection, currentTransaction)
    {
    }

    public async Task<Reader?> GetByIdAsync(long readerId, CancellationToken ct)
    {
        await EnsureOpenAsync(ct);

        const string sql = @"SELECT ReaderId, CardNumber, FullName, Email, Phone, IsDeleted,
                                     CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
                              FROM dbo.Readers WHERE ReaderId = @ReaderId AND IsDeleted = 0";

        await using var cmd = new SqlCommand(sql, Connection, Transaction);
        cmd.Parameters.Add("@ReaderId", SqlDbType.BigInt).Value = readerId;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return Map(reader);
    }

    public async Task<Reader?> GetByCardNumberAsync(string cardNumber, CancellationToken ct)
    {
        await EnsureOpenAsync(ct);

        const string sql = @"SELECT ReaderId, CardNumber, FullName, Email, Phone, IsDeleted,
                                     CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
                              FROM dbo.Readers WHERE CardNumber = @CardNumber AND IsDeleted = 0";

        await using var cmd = new SqlCommand(sql, Connection, Transaction);
        cmd.Parameters.Add("@CardNumber", SqlDbType.NVarChar, 20).Value = cardNumber;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return Map(reader);
    }

    public async Task<long> CreateAsync(Reader reader, CancellationToken ct)
    {
        await EnsureOpenAsync(ct);

        const string sql = @"INSERT INTO dbo.Readers (CardNumber, FullName, Email, Phone)
                              OUTPUT INSERTED.ReaderId
                              VALUES (@CardNumber, @FullName, @Email, @Phone)";

        await using var cmd = new SqlCommand(sql, Connection, Transaction);
        cmd.Parameters.Add("@CardNumber", SqlDbType.NVarChar, 20).Value = reader.CardNumber;
        cmd.Parameters.Add("@FullName", SqlDbType.NVarChar, 200).Value = reader.FullName;
        cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 256).Value = reader.Email;
        cmd.Parameters.Add("@Phone", SqlDbType.NVarChar, 30).Value = (object?)reader.Phone ?? DBNull.Value;

        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(result);
    }

    public async Task UpdateAsync(Reader reader, CancellationToken ct)
    {
        await EnsureOpenAsync(ct);

        const string sql = @"UPDATE dbo.Readers
                              SET FullName = @FullName, Email = @Email, Phone = @Phone,
                                  UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UpdatedBy
                              WHERE ReaderId = @ReaderId";

        await using var cmd = new SqlCommand(sql, Connection, Transaction);
        cmd.Parameters.Add("@FullName", SqlDbType.NVarChar, 200).Value = reader.FullName;
        cmd.Parameters.Add("@Email", SqlDbType.NVarChar, 256).Value = reader.Email;
        cmd.Parameters.Add("@Phone", SqlDbType.NVarChar, 30).Value = (object?)reader.Phone ?? DBNull.Value;
        cmd.Parameters.Add("@UpdatedBy", SqlDbType.NVarChar, 100).Value = reader.UpdatedBy ?? "system";
        cmd.Parameters.Add("@ReaderId", SqlDbType.BigInt).Value = reader.ReaderId;

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task SoftDeleteAsync(long readerId, string updatedBy, CancellationToken ct)
    {
        await EnsureOpenAsync(ct);

        const string sql = @"UPDATE dbo.Readers
                              SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UpdatedBy
                              WHERE ReaderId = @ReaderId";

        await using var cmd = new SqlCommand(sql, Connection, Transaction);
        cmd.Parameters.Add("@UpdatedBy", SqlDbType.NVarChar, 100).Value = updatedBy;
        cmd.Parameters.Add("@ReaderId", SqlDbType.BigInt).Value = readerId;

        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static Reader Map(SqlDataReader r) => new()
    {
        ReaderId = r.GetInt64(0),
        CardNumber = r.GetString(1),
        FullName = r.GetString(2),
        Email = r.GetString(3),
        Phone = r.IsDBNull(4) ? null : r.GetString(4),
        IsDeleted = r.GetBoolean(5),
        CreatedAt = r.GetDateTime(6),
        CreatedBy = r.GetString(7),
        UpdatedAt = r.IsDBNull(8) ? null : r.GetDateTime(8),
        UpdatedBy = r.IsDBNull(9) ? null : r.GetString(9),
    };
}

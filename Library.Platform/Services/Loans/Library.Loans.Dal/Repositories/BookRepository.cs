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

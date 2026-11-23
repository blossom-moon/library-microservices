using System.Data;
using Microsoft.Data.SqlClient;

namespace Library.Loans.Dal.Repositories;

/// <summary>
/// Спільна інфраструктура для всіх репозиторіїв (і чистого ADO.NET, і Dapper):
/// одне з'єднання на весь Unit of Work і доступ до ПОТОЧНОЇ транзакції через
/// делегат. Делегат, а не збережене поле, потрібен тому, що транзакція
/// змінюється (Begin/Commit/Rollback) вже ПІСЛЯ того, як репозиторій створено —
/// якби ми зберігали SqlTransaction напряму, репозиторій "застряг" би зі старим
/// (уже закоміченим/відкоченим) об'єктом транзакції.
/// </summary>
public abstract class RepositoryBase
{
    protected readonly SqlConnection Connection;
    private readonly Func<SqlTransaction?> _currentTransaction;

    protected RepositoryBase(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
    {
        Connection = connection;
        _currentTransaction = currentTransaction;
    }

    protected SqlTransaction? Transaction => _currentTransaction();

    protected async Task EnsureOpenAsync(CancellationToken ct)
    {
        if (Connection.State != ConnectionState.Open)
            await Connection.OpenAsync(ct);
    }
}

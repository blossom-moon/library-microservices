using System.Data;
using Microsoft.Data.SqlClient;
using Library.Loans.Dal.Interfaces;
using Library.Loans.Dal.Repositories;

namespace Library.Loans.Dal;

public class UnitOfWork : IUnitOfWork
{
    private readonly SqlConnection _connection;
    private SqlTransaction? _transaction;

    public IReaderRepository Readers { get; }
    public IBookRepository Books { get; }
    public ILoanRepository Loans { get; }

    public UnitOfWork(string connectionString)
    {
        _connection = new SqlConnection(connectionString);

        // Усі репозиторії створюються ОДИН раз і отримують ОДНЕ спільне з'єднання
        // та делегат на поточну транзакцію (() => _transaction). Завдяки делегату
        // репозиторій завжди бачить актуальний стан транзакції — навіть після
        // Commit/Rollback, коли _transaction стає null, — і не потребує
        // перестворення на кожному Begin/Commit/Rollback.
        Readers = new ReaderRepository(_connection, () => _transaction);
        Books = new BookRepository(_connection, () => _transaction);
        Loans = new LoanRepository(_connection, () => _transaction);
    }

    public async Task BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken ct)
    {
        if (_connection.State != ConnectionState.Open)
            await _connection.OpenAsync(ct);

        // READ COMMITTED — стандартний компроміс: достатньо узгоджено (не бачимо
        // незакомічених змін інших транзакцій), але без надлишкових блокувань.
        // SERIALIZABLE прибрав би аномалії читання повністю, але суттєво
        // підвищив би ризик deadlock при паралельних sp_CreateLoan/sp_ReturnBook
        // на тих самих книгах — для операцій видачі книг такий trade-off не
        // виправданий.
        _transaction = (SqlTransaction)await _connection.BeginTransactionAsync(isolationLevel, ct);
    }

    public async Task CommitAsync(CancellationToken ct)
    {
        if (_transaction == null) return;
        await _transaction.CommitAsync(ct);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackAsync(CancellationToken ct)
    {
        if (_transaction == null) return;
        await _transaction.RollbackAsync(ct);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction != null) await _transaction.DisposeAsync();
        await _connection.DisposeAsync();
    }
}

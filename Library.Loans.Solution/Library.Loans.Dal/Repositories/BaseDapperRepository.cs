using Dapper;
using Microsoft.Data.SqlClient;
using Library.Loans.Dal.Interfaces;

namespace Library.Loans.Dal.Repositories;

/// <summary>
/// Базовий клас для репозиторіїв на Dapper: спільний CRUD (Get/GetAll/Add/Delete)
/// написано один раз тут; конкретні репозиторії (BookRepository, LoanRepository)
/// додають лише специфічні запити (multi-mapping JOIN, виклики процедур тощо).
/// TableName/IdColumn — константи з коду (передаються в конструкторі конкретного
/// репозиторію), а не користувацький ввід, тому їх підстановка в текст запиту
/// безпечна проти SQL-ін'єкцій; самі значення даних завжди йдуть лише як
/// параметри (@Id, і т.д.).
/// </summary>
public abstract class BaseDapperRepository<T> : RepositoryBase, IGenericRepository<T> where T : class
{
    protected readonly string TableName;
    protected readonly string IdColumn;

    protected BaseDapperRepository(
        SqlConnection connection,
        Func<SqlTransaction?> currentTransaction,
        string tableName,
        string idColumn)
        : base(connection, currentTransaction)
    {
        TableName = tableName;
        IdColumn = idColumn;
    }

    public virtual async Task<T?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        var sql = $"SELECT * FROM {TableName} WHERE {IdColumn} = @Id";
        var command = new CommandDefinition(sql, new { Id = id }, Transaction, cancellationToken: ct);
        return await Connection.QuerySingleOrDefaultAsync<T>(command);
    }

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        var sql = $"SELECT * FROM {TableName}";
        var command = new CommandDefinition(sql, transaction: Transaction, cancellationToken: ct);
        var rows = await Connection.QueryAsync<T>(command);
        return rows.ToList();
    }

    public virtual async Task<long> AddAsync(T entity, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);

        var props = typeof(T).GetProperties()
            .Where(p => p.CanRead && !string.Equals(p.Name, IdColumn, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var columns = string.Join(", ", props.Select(p => p.Name));
        var parameters = string.Join(", ", props.Select(p => "@" + p.Name));
        var sql = $"INSERT INTO {TableName} ({columns}) OUTPUT INSERTED.{IdColumn} VALUES ({parameters})";

        var command = new CommandDefinition(sql, entity, Transaction, cancellationToken: ct);
        return await Connection.ExecuteScalarAsync<long>(command);
    }

    public virtual async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        var sql = $"DELETE FROM {TableName} WHERE {IdColumn} = @Id";
        var command = new CommandDefinition(sql, new { Id = id }, Transaction, cancellationToken: ct);
        var rows = await Connection.ExecuteAsync(command);
        return rows > 0;
    }
}

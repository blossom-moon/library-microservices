using System.Text.RegularExpressions;
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

    // Ім'я таблиці/колонки — єдине, що підставляється в текст запиту (значення
    // даних — лише параметрами). Додатково перевіряємо, що це простий
    // ідентифікатор (літери/цифри/підкреслення, опційно схема через крапку).
    private static readonly Regex SafeIdentifier =
        new(@"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)?$", RegexOptions.Compiled);

    protected BaseDapperRepository(
        SqlConnection connection,
        Func<SqlTransaction?> currentTransaction,
        string tableName,
        string idColumn)
        : base(connection, currentTransaction)
    {
        if (!SafeIdentifier.IsMatch(tableName))
            throw new ArgumentException("Invalid table name.", nameof(tableName));
        if (!SafeIdentifier.IsMatch(idColumn))
            throw new ArgumentException("Invalid id column name.", nameof(idColumn));

        TableName = tableName;
        IdColumn = idColumn;
    }

    // Лише прості властивості стають колонками INSERT; навігаційні (списки,
    // вкладені об'єкти, як Loan.Items) пропускаються.
    private static bool IsColumnType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type.IsPrimitive || type.IsEnum
            || type == typeof(string) || type == typeof(decimal)
            || type == typeof(DateTime) || type == typeof(DateTimeOffset)
            || type == typeof(Guid) || type == typeof(byte[]);
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
            .Where(p => p.CanRead && IsColumnType(p.PropertyType)
                        && !string.Equals(p.Name, IdColumn, StringComparison.OrdinalIgnoreCase))
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

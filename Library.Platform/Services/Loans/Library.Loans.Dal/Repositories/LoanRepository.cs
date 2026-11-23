using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Library.Loans.Domain;
using Library.Loans.Dal.Interfaces;

namespace Library.Loans.Dal.Repositories;

public class LoanRepository : BaseDapperRepository<Loan>, ILoanRepository
{
    public LoanRepository(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
        : base(connection, currentTransaction, "dbo.Loans", "LoanId")
    {
    }

    public async Task<long> CreateLoanAsync(long readerId, DateTime dueDate, IEnumerable<long> bookIds, CancellationToken ct)
    {
        await EnsureOpenAsync(ct);

        var table = new DataTable();
        table.Columns.Add("BookId", typeof(long));
        foreach (var id in bookIds)
            table.Rows.Add(id);

        var parameters = new DynamicParameters();
        parameters.Add("@ReaderId", readerId);
        parameters.Add("@DueDate", dueDate);
        parameters.Add("@BookIds", table.AsTableValuedParameter("dbo.BookIdList"));

        var command = new CommandDefinition(
            "dbo.sp_CreateLoan", parameters,
            Transaction, commandType: CommandType.StoredProcedure, cancellationToken: ct);

        try
        {
            return await Connection.QuerySingleAsync<long>(command);
        }
        catch (SqlException ex) when (ex.Number == 50001)
        {
            throw new NotFoundException(ex.Message);
        }
        catch (SqlException ex) when (ex.Number is 50002 or 2601 or 2627)
        {
            throw new BusinessConflictException(ex.Message);
        }
    }

    public async Task ReturnBookAsync(long loanId, long bookId, CancellationToken ct)
    {
        await EnsureOpenAsync(ct);

        var parameters = new { LoanId = loanId, BookId = bookId };
        var command = new CommandDefinition(
            "dbo.sp_ReturnBook", parameters,
            Transaction, commandType: CommandType.StoredProcedure, cancellationToken: ct);

        try
        {
            await Connection.ExecuteAsync(command);
        }
        catch (SqlException ex) when (ex.Number == 50001)
        {
            // THROW 50001 у sp_ReturnBook — видачу не знайдено
            throw new NotFoundException(ex.Message);
        }
        catch (SqlException ex) when (ex.Number is 50002 or 50003)
        {
            // THROW 50002/50003 — недопустимий перехід статусу (видача вже
            // Returned/Cancelled) або книга не входить до активних позицій видачі
            throw new BusinessConflictException(ex.Message);
        }
    }

    public async Task CancelLoanAsync(long loanId, CancellationToken ct)
    {
        await EnsureOpenAsync(ct);

        var command = new CommandDefinition(
            "dbo.sp_CancelLoan", new { LoanId = loanId },
            Transaction, commandType: CommandType.StoredProcedure, cancellationToken: ct);

        try
        {
            await Connection.ExecuteAsync(command);
        }
        catch (SqlException ex) when (ex.Number == 50001)
        {
            throw new NotFoundException(ex.Message);
        }
        catch (SqlException ex) when (ex.Number == 50002)
        {
            throw new BusinessConflictException(ex.Message);
        }
    }

    public async Task<IEnumerable<Loan>> GetActiveLoansByReaderAsync(long readerId, CancellationToken ct)
    {
        await EnsureOpenAsync(ct);

        var command = new CommandDefinition(
            "dbo.sp_GetActiveLoansByReader", new { ReaderId = readerId },
            Transaction, commandType: CommandType.StoredProcedure, cancellationToken: ct);

        return await Connection.QueryAsync<Loan>(command);
    }

    public async Task<IEnumerable<LoanItem>> GetItemsByLoanIdAsync(long loanId, CancellationToken ct)
    {
        await EnsureOpenAsync(ct);

        const string sql = @"SELECT LoanItemId, LoanId, BookId, BookTitle, ReturnedAt
                              FROM dbo.LoanItems WHERE LoanId = @LoanId";

        var command = new CommandDefinition(sql, new { LoanId = loanId }, Transaction, cancellationToken: ct);
        return await Connection.QueryAsync<LoanItem>(command);
    }

    /// <summary>
    /// Multi-mapping: один SQL-запит (LEFT JOIN Loans + LoanItems), один виклик
    /// Dapper QueryAsync&lt;Loan, LoanItem, Loan&gt; зі splitOn, замість двох
    /// окремих запитів (GetByIdAsync + GetItemsByLoanIdAsync).
    /// </summary>
    public async Task<Loan?> GetWithItemsAsync(long loanId, CancellationToken ct)
    {
        await EnsureOpenAsync(ct);

        const string sql = @"
            SELECT l.LoanId, l.ReaderId, l.Status, l.LoanDate, l.DueDate, l.ReturnDate, l.FineAmount, l.IsDeleted,
                   i.LoanItemId, i.LoanId, i.BookId, i.BookTitle, i.ReturnedAt
            FROM dbo.Loans l
            LEFT JOIN dbo.LoanItems i ON i.LoanId = l.LoanId
            WHERE l.LoanId = @LoanId";

        Loan? result = null;
        var command = new CommandDefinition(sql, new { LoanId = loanId }, Transaction, cancellationToken: ct);

        await Connection.QueryAsync<Loan, LoanItem, Loan>(
            command,
            (loan, item) =>
            {
                result ??= loan;
                if (item is not null && item.LoanItemId != 0)
                    result.Items.Add(item);
                return result;
            },
            splitOn: "LoanItemId");

        return result;
    }
}

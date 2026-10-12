/*
    Фікс для Lab01 (Library_Loans): sp_ReturnBook і sp_CancelLoan.
    Паттерн повністю аналогічний usp_ConfirmOrder зі специфікації:
      - SET XACT_ABORT ON — будь-яка помилка автоматично відкочує транзакцію;
      - BEGIN TRAN / COMMIT;
      - читання поточного статусу з WITH (UPDLOCK) — блокує рядок від
        одночасної зміни іншою транзакцією, поки ми перевіряємо й
        застосовуємо перехід статусу (захист від стану гонитви);
      - неприпустимий перехід -> RAISERROR/THROW з власним номером помилки,
        яку C#-репозиторій (LoanRepository.ReturnBookAsync/CancelLoanAsync)
        перехоплює і перетворює на NotFoundException (50001) або
        BusinessConflictException (50002/50003).

    Виконати в SSMS в контексті бази Library_Loans (перевірте dropdown!).
*/

USE Library_Loans;
GO

IF OBJECT_ID('dbo.sp_ReturnBook', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_ReturnBook;
GO

CREATE PROCEDURE dbo.sp_ReturnBook
    @LoanId BIGINT,
    @BookId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    DECLARE @Status NVARCHAR(20);

    -- UPDLOCK: блокуємо рядок видачі на час перевірки й оновлення статусу,
    -- щоб паралельний виклик не побачив застарілий статус і не "проскочив"
    -- заборонений перехід.
    SELECT @Status = Status
    FROM dbo.Loans WITH (UPDLOCK)
    WHERE LoanId = @LoanId AND IsDeleted = 0;

    IF @Status IS NULL
    BEGIN
        ROLLBACK TRAN;
        THROW 50001, 'Loan not found.', 1;
    END

    IF @Status IN ('Returned', 'Cancelled')
    BEGIN
        ROLLBACK TRAN;
        THROW 50002, 'Loan is already in a terminal status and cannot accept a return.', 1;
    END

    IF NOT EXISTS (
        SELECT 1 FROM dbo.LoanItems
        WHERE LoanId = @LoanId AND BookId = @BookId AND ReturnedAt IS NULL
    )
    BEGIN
        ROLLBACK TRAN;
        THROW 50003, 'This book is not an outstanding item of the specified loan.', 1;
    END

    UPDATE dbo.LoanItems
    SET ReturnedAt = SYSUTCDATETIME()
    WHERE LoanId = @LoanId AND BookId = @BookId;

    UPDATE dbo.Books
    SET IsAvailable = 1
    WHERE BookId = @BookId;

    -- Якщо всі позиції видачі вже повернуто — переводимо видачу в Returned.
    IF NOT EXISTS (
        SELECT 1 FROM dbo.LoanItems WHERE LoanId = @LoanId AND ReturnedAt IS NULL
    )
    BEGIN
        UPDATE dbo.Loans
        SET Status = 'Returned', ReturnDate = SYSUTCDATETIME()
        WHERE LoanId = @LoanId;
    END

    COMMIT TRAN;
END
GO

IF OBJECT_ID('dbo.sp_CancelLoan', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_CancelLoan;
GO

CREATE PROCEDURE dbo.sp_CancelLoan
    @LoanId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    DECLARE @Status NVARCHAR(20);

    SELECT @Status = Status
    FROM dbo.Loans WITH (UPDLOCK)
    WHERE LoanId = @LoanId AND IsDeleted = 0;

    IF @Status IS NULL
    BEGIN
        ROLLBACK TRAN;
        THROW 50001, 'Loan not found.', 1;
    END

    IF @Status IN ('Returned', 'Cancelled')
    BEGIN
        ROLLBACK TRAN;
        THROW 50002, 'Loan cannot be cancelled from its current status.', 1;
    END

    -- Повертаємо доступність усіх книг, що ще не були повернуті в межах цієї видачі.
    UPDATE b
    SET b.IsAvailable = 1
    FROM dbo.Books b
    JOIN dbo.LoanItems i ON i.BookId = b.BookId
    WHERE i.LoanId = @LoanId AND i.ReturnedAt IS NULL;

    UPDATE dbo.Loans
    SET Status = 'Cancelled'
    WHERE LoanId = @LoanId;

    COMMIT TRAN;
END
GO

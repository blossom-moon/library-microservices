/*
    Library_Loans — збережені процедури (Проєкт №1).
    Виконати в SSMS ПІСЛЯ schema.sql, у контексті бази Library_Loans.
*/

USE Library_Loans;
GO

IF OBJECT_ID('dbo.sp_CreateLoan', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_CreateLoan;
GO
CREATE PROCEDURE dbo.sp_CreateLoan
    @ReaderId BIGINT, @DueDate DATE, @BookIds dbo.BookIdList READONLY
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRAN;
    BEGIN TRY
        INSERT INTO dbo.Loans (ReaderId, DueDate)
        VALUES (@ReaderId, @DueDate);
        DECLARE @LoanId BIGINT = SCOPE_IDENTITY();

        INSERT INTO dbo.LoanItems (LoanId, BookId, BookTitle)
        SELECT @LoanId, b.BookId, b.Title
        FROM dbo.Books b WITH (UPDLOCK) JOIN @BookIds x ON x.BookId = b.BookId
        WHERE b.IsAvailable = 1;

        IF @@ROWCOUNT <> (SELECT COUNT(*) FROM @BookIds)
            THROW 50001, 'One or more books are not available', 1;

        UPDATE b SET IsAvailable = 0
        FROM dbo.Books b JOIN @BookIds x ON x.BookId = b.BookId;

        INSERT INTO dbo.LoanStatusHistory (LoanId, OldStatus, NewStatus)
        VALUES (@LoanId, NULL, 'Active');

        COMMIT TRAN;
        SELECT @LoanId AS LoanId;
    END TRY
    BEGIN CATCH
        ROLLBACK TRAN;
        THROW;
    END CATCH
END;
GO

-- Транзакційна зміна статусу з UPDLOCK-захистом від одночасних запитів і
-- власними кодами помилок (50001 = не знайдено, 50002/50003 = неприпустимий
-- перехід статусу).
IF OBJECT_ID('dbo.sp_ReturnBook', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_ReturnBook;
GO
CREATE PROCEDURE dbo.sp_ReturnBook
    @LoanId BIGINT, @BookId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRAN;

    DECLARE @Status NVARCHAR(20);
    SELECT @Status = Status FROM dbo.Loans WITH (UPDLOCK) WHERE LoanId = @LoanId AND IsDeleted = 0;

    IF @Status IS NULL
    BEGIN
        ROLLBACK TRAN;
        THROW 50001, 'Loan not found.', 1;
    END

    IF @Status IN ('Returned', 'Cancelled')
    BEGIN
        ROLLBACK TRAN;
        THROW 50002, 'Loan is already in a terminal status.', 1;
    END

    IF NOT EXISTS (SELECT 1 FROM dbo.LoanItems WHERE LoanId = @LoanId AND BookId = @BookId AND ReturnedAt IS NULL)
    BEGIN
        ROLLBACK TRAN;
        THROW 50003, 'This book is not an outstanding item of the specified loan.', 1;
    END

    UPDATE dbo.LoanItems SET ReturnedAt = SYSUTCDATETIME()
    WHERE LoanId = @LoanId AND BookId = @BookId;

    UPDATE dbo.Books SET IsAvailable = 1 WHERE BookId = @BookId;

    IF NOT EXISTS (SELECT 1 FROM dbo.LoanItems WHERE LoanId = @LoanId AND ReturnedAt IS NULL)
    BEGIN
        UPDATE dbo.Loans SET Status = 'Returned', ReturnDate = CAST(SYSUTCDATETIME() AS DATE)
        WHERE LoanId = @LoanId;

        INSERT INTO dbo.LoanStatusHistory (LoanId, OldStatus, NewStatus)
        VALUES (@LoanId, 'Active', 'Returned');
    END

    COMMIT TRAN;
END;
GO

IF OBJECT_ID('dbo.sp_CancelLoan', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_CancelLoan;
GO
CREATE PROCEDURE dbo.sp_CancelLoan
    @LoanId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRAN;

    DECLARE @Status NVARCHAR(20);
    SELECT @Status = Status FROM dbo.Loans WITH (UPDLOCK) WHERE LoanId = @LoanId AND IsDeleted = 0;

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

    UPDATE b SET b.IsAvailable = 1
    FROM dbo.Books b JOIN dbo.LoanItems li ON li.BookId = b.BookId
    WHERE li.LoanId = @LoanId AND li.ReturnedAt IS NULL;

    UPDATE dbo.Loans SET Status = 'Cancelled' WHERE LoanId = @LoanId;

    INSERT INTO dbo.LoanStatusHistory (LoanId, OldStatus, NewStatus)
    VALUES (@LoanId, 'Active', 'Cancelled');

    COMMIT TRAN;
END;
GO

IF OBJECT_ID('dbo.sp_GetActiveLoansByReader', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_GetActiveLoansByReader;
GO
CREATE PROCEDURE dbo.sp_GetActiveLoansByReader
    @ReaderId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT l.LoanId, l.LoanDate, l.DueDate, l.Status, li.BookId, li.BookTitle
    FROM dbo.Loans l
    JOIN dbo.LoanItems li ON li.LoanId = l.LoanId
    WHERE l.ReaderId = @ReaderId AND l.Status IN ('Active', 'Overdue') AND l.IsDeleted = 0;
END;
GO

IF OBJECT_ID('dbo.sp_GetOverdueLoans', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_GetOverdueLoans;
GO
CREATE PROCEDURE dbo.sp_GetOverdueLoans
AS
BEGIN
    SET NOCOUNT ON;
    SELECT LoanId, ReaderId, DueDate
    FROM dbo.Loans
    WHERE ReturnDate IS NULL AND DueDate < CAST(SYSUTCDATETIME() AS DATE) AND IsDeleted = 0;
END;
GO

IF OBJECT_ID('dbo.sp_CalculateFine', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_CalculateFine;
GO
CREATE PROCEDURE dbo.sp_CalculateFine
    @LoanId BIGINT, @DailyRate DECIMAL(10,2) = 2.00
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Loans
    SET FineAmount = @DailyRate * DATEDIFF(DAY, DueDate, SYSUTCDATETIME()),
        Status = 'Overdue'
    WHERE LoanId = @LoanId AND ReturnDate IS NULL AND DueDate < CAST(SYSUTCDATETIME() AS DATE);
END;
GO

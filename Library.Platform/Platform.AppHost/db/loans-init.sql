-- Автогенерований скрипт ініціалізації БД LibraryLoans (Lab02: schema + procedures + seed).
-- Aspire виконує WithCreationScript одним пакетом (без підтримки GO), тому кожен пакет
-- Lab02-скрипта виконується окремо через sp_executesql у контексті бази.
IF DB_ID(N'LibraryLoans') IS NULL CREATE DATABASE [LibraryLoans];

EXEC [LibraryLoans].sys.sp_executesql N'-- Табличний тип для передачі списку книг у sp_CreateLoan одним запитом
IF TYPE_ID(N''dbo.BookIdList'') IS NULL
    CREATE TYPE dbo.BookIdList AS TABLE(
        BookId BIGINT NOT NULL,
        PRIMARY KEY CLUSTERED (BookId ASC) WITH (IGNORE_DUP_KEY = OFF)
    );';

EXEC [LibraryLoans].sys.sp_executesql N'-- Books: локальна копія книг із каталогу (Library.Catalog) — поле IsAvailable
-- є локальним станом (чи книга зараз видана), а не каталожним атрибутом.
IF OBJECT_ID(''dbo.Books'', ''U'') IS NULL
CREATE TABLE dbo.Books(
    BookId      BIGINT          NOT NULL,
    Isbn        NVARCHAR(20)    NOT NULL,
    Title       NVARCHAR(300)   NOT NULL,
    IsAvailable BIT             NOT NULL CONSTRAINT DF_Books_Avail DEFAULT (1),
    SyncedAt    DATETIME2(7)    NOT NULL CONSTRAINT DF_Books_Synced DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Books PRIMARY KEY CLUSTERED (BookId ASC),
    CONSTRAINT UQ_Books_Isbn UNIQUE NONCLUSTERED (Isbn ASC)
);';

EXEC [LibraryLoans].sys.sp_executesql N'-- Readers: читачі бібліотеки (1:N до Loans)
IF OBJECT_ID(''dbo.Readers'', ''U'') IS NULL
CREATE TABLE dbo.Readers(
    ReaderId    BIGINT IDENTITY(1,1) NOT NULL,
    CardNumber  NVARCHAR(20)  NOT NULL,
    FullName    NVARCHAR(200) NOT NULL,
    Email       NVARCHAR(256) NOT NULL,
    Phone       NVARCHAR(30)  NULL,
    IsDeleted   BIT NOT NULL CONSTRAINT DF_Readers_IsDeleted DEFAULT (0),
    CreatedAt   DATETIME2(7) NOT NULL CONSTRAINT DF_Readers_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy   NVARCHAR(100) NOT NULL CONSTRAINT DF_Readers_CreatedBy DEFAULT (''system''),
    UpdatedAt   DATETIME2(7) NULL,
    UpdatedBy   NVARCHAR(100) NULL,
    CONSTRAINT PK_Readers PRIMARY KEY CLUSTERED (ReaderId ASC),
    CONSTRAINT UQ_Readers_Card UNIQUE NONCLUSTERED (CardNumber ASC),
    CONSTRAINT UQ_Readers_Email UNIQUE NONCLUSTERED (Email ASC)
);';

EXEC [LibraryLoans].sys.sp_executesql N'-- Loans: видачі (N:1 до Readers). RowVer — оптимістична конкурентність.
IF OBJECT_ID(''dbo.Loans'', ''U'') IS NULL
CREATE TABLE dbo.Loans(
    LoanId      BIGINT IDENTITY(1,1) NOT NULL,
    ReaderId    BIGINT NOT NULL,
    Status      NVARCHAR(20) NOT NULL CONSTRAINT DF_Loans_Status DEFAULT (''Active''),
    LoanDate    DATE NOT NULL CONSTRAINT DF_Loans_LoanDate DEFAULT (CONVERT(DATE, SYSUTCDATETIME())),
    DueDate     DATE NOT NULL,
    ReturnDate  DATE NULL,
    FineAmount  DECIMAL(10,2) NOT NULL CONSTRAINT DF_Loans_Fine DEFAULT (0),
    IsDeleted   BIT NOT NULL CONSTRAINT DF_Loans_IsDeleted DEFAULT (0),
    CreatedAt   DATETIME2(7) NOT NULL CONSTRAINT DF_Loans_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy   NVARCHAR(100) NOT NULL CONSTRAINT DF_Loans_CreatedBy DEFAULT (''system''),
    UpdatedAt   DATETIME2(7) NULL,
    UpdatedBy   NVARCHAR(100) NULL,
    RowVer      ROWVERSION NOT NULL,
    CONSTRAINT PK_Loans PRIMARY KEY CLUSTERED (LoanId ASC),
    CONSTRAINT CK_Loans_Dates CHECK (DueDate >= LoanDate),
    CONSTRAINT CK_Loans_Fine CHECK (FineAmount >= 0),
    CONSTRAINT CK_Loans_Status CHECK (Status IN (''Active'',''Overdue'',''Returned'',''Cancelled'')),
    CONSTRAINT FK_Loans_Readers FOREIGN KEY (ReaderId) REFERENCES dbo.Readers(ReaderId)
);';

EXEC [LibraryLoans].sys.sp_executesql N'-- LoanDetails: 1:1 до Loans — додаткові дані видачі
IF OBJECT_ID(''dbo.LoanDetails'', ''U'') IS NULL
CREATE TABLE dbo.LoanDetails(
    LoanId      BIGINT NOT NULL,
    PickupPoint NVARCHAR(100) NOT NULL CONSTRAINT DF_LD_Pickup DEFAULT (N''Головний зал''),
    Comment     NVARCHAR(500) NULL,
    CONSTRAINT PK_LoanDetails PRIMARY KEY CLUSTERED (LoanId ASC),
    CONSTRAINT FK_LoanDetails_Loans FOREIGN KEY (LoanId) REFERENCES dbo.Loans(LoanId) ON DELETE CASCADE
);';

EXEC [LibraryLoans].sys.sp_executesql N'-- LoanItems: M:N між Loans і Books (власні поля: BookTitle-знімок, ReturnedAt)
IF OBJECT_ID(''dbo.LoanItems'', ''U'') IS NULL
CREATE TABLE dbo.LoanItems(
    LoanItemId  BIGINT IDENTITY(1,1) NOT NULL,
    LoanId      BIGINT NOT NULL,
    BookId      BIGINT NOT NULL,
    BookTitle   NVARCHAR(300) NOT NULL,
    ReturnedAt  DATETIME2(7) NULL,
    CONSTRAINT PK_LoanItems PRIMARY KEY CLUSTERED (LoanItemId ASC),
    CONSTRAINT UQ_LoanItems_Loan_Book UNIQUE NONCLUSTERED (LoanId ASC, BookId ASC),
    CONSTRAINT FK_LoanItems_Loans FOREIGN KEY (LoanId) REFERENCES dbo.Loans(LoanId) ON DELETE CASCADE,
    CONSTRAINT FK_LoanItems_Books FOREIGN KEY (BookId) REFERENCES dbo.Books(BookId)
);';

EXEC [LibraryLoans].sys.sp_executesql N'-- LoanStatusHistory: аудит-журнал переходів статусу видачі (1:N до Loans)
IF OBJECT_ID(''dbo.LoanStatusHistory'', ''U'') IS NULL
CREATE TABLE dbo.LoanStatusHistory(
    HistoryId   BIGINT IDENTITY(1,1) NOT NULL,
    LoanId      BIGINT NOT NULL,
    OldStatus   NVARCHAR(20) NULL,
    NewStatus   NVARCHAR(20) NOT NULL,
    ChangedAt   DATETIME2(7) NOT NULL CONSTRAINT DF_LSH_At DEFAULT (SYSUTCDATETIME()),
    ChangedBy   NVARCHAR(100) NOT NULL CONSTRAINT DF_LSH_By DEFAULT (''system''),
    CONSTRAINT PK_LSH PRIMARY KEY CLUSTERED (HistoryId ASC),
    CONSTRAINT FK_LSH_Loans FOREIGN KEY (LoanId) REFERENCES dbo.Loans(LoanId)
);';

EXEC [LibraryLoans].sys.sp_executesql N'-- ---------------------------------------------------------------------------
-- Індекси — кожен під конкретний запит
-- ---------------------------------------------------------------------------

-- Запит: sp_GetActiveLoansByReader — "активні видачі читача"
--   WHERE ReaderId = @ReaderId AND Status IN (''Active'',''Overdue'') AND IsDeleted = 0.
-- Ключ (ReaderId, Status) — пошук за читачем і статусом; INCLUDE (LoanDate,
-- DueDate) робить індекс покриваючим (без key lookup); фільтр IsDeleted = 0
-- робить його вужчим — м''яко видалені видачі в індекс не потрапляють.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = ''IX_Loans_Reader_Status'')
    CREATE NONCLUSTERED INDEX IX_Loans_Reader_Status ON dbo.Loans(ReaderId ASC, Status ASC)
    INCLUDE (LoanDate, DueDate)
    WHERE (IsDeleted = 0);';

EXEC [LibraryLoans].sys.sp_executesql N'-- Запит: історія статусів видачі, відсортована за часом
--   WHERE LoanId = @LoanId ORDER BY ChangedAt.
-- Складений ключ (LoanId, ChangedAt) віддає рядки вже в потрібному порядку.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = ''IX_LSH_Loan'')
    CREATE NONCLUSTERED INDEX IX_LSH_Loan ON dbo.LoanStatusHistory(LoanId ASC, ChangedAt ASC);';

EXEC [LibraryLoans].sys.sp_executesql N'-- ---------------------------------------------------------------------------
-- Каскади (ON DELETE) — усвідомлений вибір
-- ---------------------------------------------------------------------------
-- LoanDetails -> Loans  : CASCADE. Це 1:1 залежна таблиця, яка без видачі
--                         не має сенсу.
-- LoanItems   -> Loans  : CASCADE. Позиції — складова частина видачі.
-- LoanItems   -> Books  : без каскаду (NO ACTION). Видалення книги не повинно
--                         мовчки стирати історію видач.
-- Loans       -> Readers: без каскаду. Читачів не видаляємо фізично — діє
--                         м''яке видалення (IsDeleted).
-- LoanStatusHistory -> Loans: без каскаду. Це аудит-журнал, його не можна
--                         втрачати разом з видачею.
-- Унікальні обмеження: UQ_Books_Isbn (природний ключ книги, синхронізація з
-- Catalog), UQ_Readers_Card / UQ_Readers_Email (один читач — одна картка й
-- один email), UQ_LoanItems_Loan_Book (книга не може двічі бути в одній видачі).';

EXEC [LibraryLoans].sys.sp_executesql N'IF OBJECT_ID(''dbo.sp_CreateLoan'', ''P'') IS NOT NULL DROP PROCEDURE dbo.sp_CreateLoan;';

EXEC [LibraryLoans].sys.sp_executesql N'CREATE PROCEDURE dbo.sp_CreateLoan
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
            THROW 50001, ''One or more books are not available'', 1;

        UPDATE b SET IsAvailable = 0
        FROM dbo.Books b JOIN @BookIds x ON x.BookId = b.BookId;

        INSERT INTO dbo.LoanStatusHistory (LoanId, OldStatus, NewStatus)
        VALUES (@LoanId, NULL, ''Active'');

        COMMIT TRAN;
        SELECT @LoanId AS LoanId;
    END TRY
    BEGIN CATCH
        ROLLBACK TRAN;
        THROW;
    END CATCH
END;';

EXEC [LibraryLoans].sys.sp_executesql N'-- Транзакційна зміна статусу з UPDLOCK-захистом від одночасних запитів і
-- власними кодами помилок (50001 = не знайдено, 50002/50003 = неприпустимий
-- перехід статусу).
IF OBJECT_ID(''dbo.sp_ReturnBook'', ''P'') IS NOT NULL DROP PROCEDURE dbo.sp_ReturnBook;';

EXEC [LibraryLoans].sys.sp_executesql N'CREATE PROCEDURE dbo.sp_ReturnBook
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
        THROW 50001, ''Loan not found.'', 1;
    END

    IF @Status IN (''Returned'', ''Cancelled'')
    BEGIN
        ROLLBACK TRAN;
        THROW 50002, ''Loan is already in a terminal status.'', 1;
    END

    IF NOT EXISTS (SELECT 1 FROM dbo.LoanItems WHERE LoanId = @LoanId AND BookId = @BookId AND ReturnedAt IS NULL)
    BEGIN
        ROLLBACK TRAN;
        THROW 50003, ''This book is not an outstanding item of the specified loan.'', 1;
    END

    UPDATE dbo.LoanItems SET ReturnedAt = SYSUTCDATETIME()
    WHERE LoanId = @LoanId AND BookId = @BookId;

    UPDATE dbo.Books SET IsAvailable = 1 WHERE BookId = @BookId;

    IF NOT EXISTS (SELECT 1 FROM dbo.LoanItems WHERE LoanId = @LoanId AND ReturnedAt IS NULL)
    BEGIN
        UPDATE dbo.Loans SET Status = ''Returned'', ReturnDate = CAST(SYSUTCDATETIME() AS DATE)
        WHERE LoanId = @LoanId;

        INSERT INTO dbo.LoanStatusHistory (LoanId, OldStatus, NewStatus)
        VALUES (@LoanId, ''Active'', ''Returned'');
    END

    COMMIT TRAN;
END;';

EXEC [LibraryLoans].sys.sp_executesql N'IF OBJECT_ID(''dbo.sp_CancelLoan'', ''P'') IS NOT NULL DROP PROCEDURE dbo.sp_CancelLoan;';

EXEC [LibraryLoans].sys.sp_executesql N'CREATE PROCEDURE dbo.sp_CancelLoan
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
        THROW 50001, ''Loan not found.'', 1;
    END

    IF @Status IN (''Returned'', ''Cancelled'')
    BEGIN
        ROLLBACK TRAN;
        THROW 50002, ''Loan cannot be cancelled from its current status.'', 1;
    END

    UPDATE b SET b.IsAvailable = 1
    FROM dbo.Books b JOIN dbo.LoanItems li ON li.BookId = b.BookId
    WHERE li.LoanId = @LoanId AND li.ReturnedAt IS NULL;

    UPDATE dbo.Loans SET Status = ''Cancelled'' WHERE LoanId = @LoanId;

    INSERT INTO dbo.LoanStatusHistory (LoanId, OldStatus, NewStatus)
    VALUES (@LoanId, ''Active'', ''Cancelled'');

    COMMIT TRAN;
END;';

EXEC [LibraryLoans].sys.sp_executesql N'IF OBJECT_ID(''dbo.sp_GetActiveLoansByReader'', ''P'') IS NOT NULL DROP PROCEDURE dbo.sp_GetActiveLoansByReader;';

EXEC [LibraryLoans].sys.sp_executesql N'CREATE PROCEDURE dbo.sp_GetActiveLoansByReader
    @ReaderId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT l.LoanId, l.LoanDate, l.DueDate, l.Status, li.BookId, li.BookTitle
    FROM dbo.Loans l
    JOIN dbo.LoanItems li ON li.LoanId = l.LoanId
    WHERE l.ReaderId = @ReaderId AND l.Status IN (''Active'', ''Overdue'') AND l.IsDeleted = 0;
END;';

EXEC [LibraryLoans].sys.sp_executesql N'IF OBJECT_ID(''dbo.sp_GetOverdueLoans'', ''P'') IS NOT NULL DROP PROCEDURE dbo.sp_GetOverdueLoans;';

EXEC [LibraryLoans].sys.sp_executesql N'CREATE PROCEDURE dbo.sp_GetOverdueLoans
AS
BEGIN
    SET NOCOUNT ON;
    SELECT LoanId, ReaderId, DueDate
    FROM dbo.Loans
    WHERE ReturnDate IS NULL AND DueDate < CAST(SYSUTCDATETIME() AS DATE) AND IsDeleted = 0;
END;';

EXEC [LibraryLoans].sys.sp_executesql N'IF OBJECT_ID(''dbo.sp_CalculateFine'', ''P'') IS NOT NULL DROP PROCEDURE dbo.sp_CalculateFine;';

EXEC [LibraryLoans].sys.sp_executesql N'CREATE PROCEDURE dbo.sp_CalculateFine
    @LoanId BIGINT, @DailyRate DECIMAL(10,2) = 2.00
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Loans
    SET FineAmount = @DailyRate * DATEDIFF(DAY, DueDate, SYSUTCDATETIME()),
        Status = ''Overdue''
    WHERE LoanId = @LoanId AND ReturnDate IS NULL AND DueDate < CAST(SYSUTCDATETIME() AS DATE);
END;';

EXEC [LibraryLoans].sys.sp_executesql N'IF NOT EXISTS (SELECT 1 FROM dbo.Readers WHERE CardNumber = ''RDR-0001'' OR Email = ''kostetska.kristina2005@gmail.com'')
    INSERT INTO dbo.Readers (CardNumber, FullName, Email, Phone)
    VALUES (''RDR-0001'', N''Христина Костецька'', ''kostetska.kristina2005@gmail.com'', ''+380000000001'');

IF NOT EXISTS (SELECT 1 FROM dbo.Readers WHERE CardNumber = ''RDR-0002'' OR Email = ''ivan.petrenko@example.com'')
    INSERT INTO dbo.Readers (CardNumber, FullName, Email, Phone)
    VALUES (''RDR-0002'', N''Іван Петренко'', ''ivan.petrenko@example.com'', ''+380000000002'');

IF NOT EXISTS (SELECT 1 FROM dbo.Readers WHERE CardNumber = ''RDR-0003'' OR Email = ''olena.koval@example.com'')
    INSERT INTO dbo.Readers (CardNumber, FullName, Email, Phone)
    VALUES (''RDR-0003'', N''Олена Коваль'', ''olena.koval@example.com'', NULL);

-- BookId у Books не є IDENTITY (значення приходять з Library.Catalog), тому
-- вставляємо їх явно без SET IDENTITY_INSERT.
IF NOT EXISTS (SELECT 1 FROM dbo.Books WHERE BookId = 1)
    INSERT INTO dbo.Books (BookId, Isbn, Title, IsAvailable)
    VALUES (1, ''978-966-03-1234-5'', N''Кобзар'', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Books WHERE BookId = 2)
    INSERT INTO dbo.Books (BookId, Isbn, Title, IsAvailable)
    VALUES (2, ''978-966-03-5678-9'', N''Тигролови'', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Books WHERE BookId = 3)
    INSERT INTO dbo.Books (BookId, Isbn, Title, IsAvailable)
    VALUES (3, ''978-617-12-9876-5'', N''Місто'', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Books WHERE BookId = 4)
    INSERT INTO dbo.Books (BookId, Isbn, Title, IsAvailable)
    VALUES (4, ''978-617-12-4321-0'', N''Хіба ревуть воли, як ясла повні?'', 1);';

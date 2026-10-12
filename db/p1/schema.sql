/*
    Library_Loans — схема бази (Проєкт №1).
    Виконати в SSMS. Якщо бази ще нема — створи порожню базу Library_Loans
    вручну (праворуч клік на Databases → New Database), потім виконай цей
    скрипт у її контексті.
*/

USE Library_Loans;
GO

-- Табличний тип для передачі списку книг у sp_CreateLoan одним запитом
IF TYPE_ID(N'dbo.BookIdList') IS NULL
    CREATE TYPE dbo.BookIdList AS TABLE(
        BookId BIGINT NOT NULL,
        PRIMARY KEY CLUSTERED (BookId ASC) WITH (IGNORE_DUP_KEY = OFF)
    );
GO

-- Books: локальна копія книг із каталогу (Library.Catalog) — поле IsAvailable
-- є локальним станом (чи книга зараз видана), а не каталожним атрибутом.
IF OBJECT_ID('dbo.Books', 'U') IS NULL
CREATE TABLE dbo.Books(
    BookId      BIGINT          NOT NULL,
    Isbn        NVARCHAR(20)    NOT NULL,
    Title       NVARCHAR(300)   NOT NULL,
    IsAvailable BIT             NOT NULL CONSTRAINT DF_Books_Avail DEFAULT (1),
    SyncedAt    DATETIME2(7)    NOT NULL CONSTRAINT DF_Books_Synced DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Books PRIMARY KEY CLUSTERED (BookId ASC),
    CONSTRAINT UQ_Books_Isbn UNIQUE NONCLUSTERED (Isbn ASC)
);
GO

-- Readers: читачі бібліотеки (1:N до Loans)
IF OBJECT_ID('dbo.Readers', 'U') IS NULL
CREATE TABLE dbo.Readers(
    ReaderId    BIGINT IDENTITY(1,1) NOT NULL,
    CardNumber  NVARCHAR(20)  NOT NULL,
    FullName    NVARCHAR(200) NOT NULL,
    Email       NVARCHAR(256) NOT NULL,
    Phone       NVARCHAR(30)  NULL,
    IsDeleted   BIT NOT NULL CONSTRAINT DF_Readers_IsDeleted DEFAULT (0),
    CreatedAt   DATETIME2(7) NOT NULL CONSTRAINT DF_Readers_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy   NVARCHAR(100) NOT NULL CONSTRAINT DF_Readers_CreatedBy DEFAULT ('system'),
    UpdatedAt   DATETIME2(7) NULL,
    UpdatedBy   NVARCHAR(100) NULL,
    CONSTRAINT PK_Readers PRIMARY KEY CLUSTERED (ReaderId ASC),
    CONSTRAINT UQ_Readers_Card UNIQUE NONCLUSTERED (CardNumber ASC),
    CONSTRAINT UQ_Readers_Email UNIQUE NONCLUSTERED (Email ASC)
);
GO

-- Loans: видачі (N:1 до Readers). RowVer — оптимістична конкурентність.
IF OBJECT_ID('dbo.Loans', 'U') IS NULL
CREATE TABLE dbo.Loans(
    LoanId      BIGINT IDENTITY(1,1) NOT NULL,
    ReaderId    BIGINT NOT NULL,
    Status      NVARCHAR(20) NOT NULL CONSTRAINT DF_Loans_Status DEFAULT ('Active'),
    LoanDate    DATE NOT NULL CONSTRAINT DF_Loans_LoanDate DEFAULT (CONVERT(DATE, SYSUTCDATETIME())),
    DueDate     DATE NOT NULL,
    ReturnDate  DATE NULL,
    FineAmount  DECIMAL(10,2) NOT NULL CONSTRAINT DF_Loans_Fine DEFAULT (0),
    IsDeleted   BIT NOT NULL CONSTRAINT DF_Loans_IsDeleted DEFAULT (0),
    CreatedAt   DATETIME2(7) NOT NULL CONSTRAINT DF_Loans_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy   NVARCHAR(100) NOT NULL CONSTRAINT DF_Loans_CreatedBy DEFAULT ('system'),
    UpdatedAt   DATETIME2(7) NULL,
    UpdatedBy   NVARCHAR(100) NULL,
    RowVer      ROWVERSION NOT NULL,
    CONSTRAINT PK_Loans PRIMARY KEY CLUSTERED (LoanId ASC),
    CONSTRAINT CK_Loans_Dates CHECK (DueDate >= LoanDate),
    CONSTRAINT CK_Loans_Fine CHECK (FineAmount >= 0),
    CONSTRAINT CK_Loans_Status CHECK (Status IN ('Active','Overdue','Returned','Cancelled')),
    CONSTRAINT FK_Loans_Readers FOREIGN KEY (ReaderId) REFERENCES dbo.Readers(ReaderId)
);
GO

-- LoanDetails: 1:1 до Loans — додаткові дані видачі
IF OBJECT_ID('dbo.LoanDetails', 'U') IS NULL
CREATE TABLE dbo.LoanDetails(
    LoanId      BIGINT NOT NULL,
    PickupPoint NVARCHAR(100) NOT NULL CONSTRAINT DF_LD_Pickup DEFAULT (N'Головний зал'),
    Comment     NVARCHAR(500) NULL,
    CONSTRAINT PK_LoanDetails PRIMARY KEY CLUSTERED (LoanId ASC),
    CONSTRAINT FK_LoanDetails_Loans FOREIGN KEY (LoanId) REFERENCES dbo.Loans(LoanId) ON DELETE CASCADE
);
GO

-- LoanItems: M:N між Loans і Books (власні поля: BookTitle-знімок, ReturnedAt)
IF OBJECT_ID('dbo.LoanItems', 'U') IS NULL
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
);
GO

-- LoanStatusHistory: аудит-журнал переходів статусу видачі (1:N до Loans)
IF OBJECT_ID('dbo.LoanStatusHistory', 'U') IS NULL
CREATE TABLE dbo.LoanStatusHistory(
    HistoryId   BIGINT IDENTITY(1,1) NOT NULL,
    LoanId      BIGINT NOT NULL,
    OldStatus   NVARCHAR(20) NULL,
    NewStatus   NVARCHAR(20) NOT NULL,
    ChangedAt   DATETIME2(7) NOT NULL CONSTRAINT DF_LSH_At DEFAULT (SYSUTCDATETIME()),
    ChangedBy   NVARCHAR(100) NOT NULL CONSTRAINT DF_LSH_By DEFAULT ('system'),
    CONSTRAINT PK_LSH PRIMARY KEY CLUSTERED (HistoryId ASC),
    CONSTRAINT FK_LSH_Loans FOREIGN KEY (LoanId) REFERENCES dbo.Loans(LoanId)
);
GO

-- ---------------------------------------------------------------------------
-- Індекси — кожен під конкретний запит
-- ---------------------------------------------------------------------------

-- Запит: sp_GetActiveLoansByReader — "активні видачі читача"
--   WHERE ReaderId = @ReaderId AND Status IN ('Active','Overdue') AND IsDeleted = 0.
-- Ключ (ReaderId, Status) — пошук за читачем і статусом; INCLUDE (LoanDate,
-- DueDate) робить індекс покриваючим (без key lookup); фільтр IsDeleted = 0
-- робить його вужчим — м'яко видалені видачі в індекс не потрапляють.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Loans_Reader_Status')
    CREATE NONCLUSTERED INDEX IX_Loans_Reader_Status ON dbo.Loans(ReaderId ASC, Status ASC)
    INCLUDE (LoanDate, DueDate)
    WHERE (IsDeleted = 0);
GO

-- Запит: історія статусів видачі, відсортована за часом
--   WHERE LoanId = @LoanId ORDER BY ChangedAt.
-- Складений ключ (LoanId, ChangedAt) віддає рядки вже в потрібному порядку.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_LSH_Loan')
    CREATE NONCLUSTERED INDEX IX_LSH_Loan ON dbo.LoanStatusHistory(LoanId ASC, ChangedAt ASC);
GO

-- ---------------------------------------------------------------------------
-- Каскади (ON DELETE) — усвідомлений вибір
-- ---------------------------------------------------------------------------
-- LoanDetails -> Loans  : CASCADE. Це 1:1 залежна таблиця, яка без видачі
--                         не має сенсу.
-- LoanItems   -> Loans  : CASCADE. Позиції — складова частина видачі.
-- LoanItems   -> Books  : без каскаду (NO ACTION). Видалення книги не повинно
--                         мовчки стирати історію видач.
-- Loans       -> Readers: без каскаду. Читачів не видаляємо фізично — діє
--                         м'яке видалення (IsDeleted).
-- LoanStatusHistory -> Loans: без каскаду. Це аудит-журнал, його не можна
--                         втрачати разом з видачею.
-- Унікальні обмеження: UQ_Books_Isbn (природний ключ книги, синхронізація з
-- Catalog), UQ_Readers_Card / UQ_Readers_Email (один читач — одна картка й
-- один email), UQ_LoanItems_Loan_Book (книга не може двічі бути в одній видачі).

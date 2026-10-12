/*
    Library_Catalog — схема бази (Проєкт №2, EF Core Code-First).
    Виконати в SSMS. Якщо бази ще нема — створи порожню базу Library_Catalog
    вручну, потім виконай цей скрипт у її контексті.
*/

USE Library_Catalog;
GO

IF OBJECT_ID('dbo.Publishers', 'U') IS NULL
CREATE TABLE dbo.Publishers(
    PublisherId BIGINT IDENTITY(1,1) NOT NULL,
    Name        NVARCHAR(MAX) NOT NULL,
    Country     NVARCHAR(MAX) NOT NULL,
    CONSTRAINT PK_Publishers PRIMARY KEY CLUSTERED (PublisherId ASC)
);
GO

IF OBJECT_ID('dbo.Categories', 'U') IS NULL
CREATE TABLE dbo.Categories(
    CategoryId BIGINT IDENTITY(1,1) NOT NULL,
    Name       NVARCHAR(450) NOT NULL,
    CONSTRAINT PK_Categories PRIMARY KEY CLUSTERED (CategoryId ASC)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Categories_Name')
    CREATE UNIQUE NONCLUSTERED INDEX IX_Categories_Name ON dbo.Categories(Name ASC);
GO

IF OBJECT_ID('dbo.Authors', 'U') IS NULL
CREATE TABLE dbo.Authors(
    AuthorId BIGINT IDENTITY(1,1) NOT NULL,
    FullName NVARCHAR(MAX) NOT NULL,
    CONSTRAINT PK_Authors PRIMARY KEY CLUSTERED (AuthorId ASC)
);
GO

-- Books: N:1 до Publishers, N:1 до Categories
IF OBJECT_ID('dbo.Books', 'U') IS NULL
CREATE TABLE dbo.Books(
    BookId      BIGINT IDENTITY(1,1) NOT NULL,
    Isbn        NVARCHAR(450) NOT NULL,
    Title       NVARCHAR(450) NOT NULL,
    Price       DECIMAL(10,2) NOT NULL,
    PublisherId BIGINT NOT NULL,
    CategoryId  BIGINT NOT NULL,
    CreatedAt   DATETIME2(7) NOT NULL CONSTRAINT DF_Books_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CreatedBy   NVARCHAR(MAX) NOT NULL CONSTRAINT DF_Books_CreatedBy DEFAULT (N'system'),
    IsDeleted   BIT NOT NULL CONSTRAINT DF_Books_IsDeleted DEFAULT (0),
    RowVersion  ROWVERSION NOT NULL,
    UpdatedAt   DATETIME2(7) NULL,
    UpdatedBy   NVARCHAR(MAX) NULL,
    CONSTRAINT PK_Books PRIMARY KEY CLUSTERED (BookId ASC),
    CONSTRAINT CK_Book_Price CHECK (Price >= 0),
    CONSTRAINT FK_Books_Publishers_PublisherId FOREIGN KEY (PublisherId) REFERENCES dbo.Publishers(PublisherId),
    CONSTRAINT FK_Books_Categories_CategoryId FOREIGN KEY (CategoryId) REFERENCES dbo.Categories(CategoryId)
);
GO
-- Індекси під конкретні запити:
-- IX_Books_Isbn (UNIQUE)  — пошук книги за ISBN і гарантія унікальності;
--                           ISBN — природний ключ, за яким Loans синхронізує копію.
-- IX_Books_Title          — пошук і сортування книг за назвою.
-- IX_Books_PublisherId    — "усі книги видавця" та JOIN до Publishers.
-- IX_Books_CategoryId     — "усі книги категорії" та JOIN до Categories.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Books_Isbn')
    CREATE UNIQUE NONCLUSTERED INDEX IX_Books_Isbn ON dbo.Books(Isbn ASC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Books_Title')
    CREATE NONCLUSTERED INDEX IX_Books_Title ON dbo.Books(Title ASC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Books_PublisherId')
    CREATE NONCLUSTERED INDEX IX_Books_PublisherId ON dbo.Books(PublisherId ASC);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Books_CategoryId')
    CREATE NONCLUSTERED INDEX IX_Books_CategoryId ON dbo.Books(CategoryId ASC);
GO

-- BookDetails: 1:1 до Books
IF OBJECT_ID('dbo.BookDetails', 'U') IS NULL
CREATE TABLE dbo.BookDetails(
    BookId     BIGINT NOT NULL,
    Annotation NVARCHAR(MAX) NOT NULL,
    PageCount  INT NOT NULL,
    Language   NVARCHAR(MAX) NOT NULL,
    CONSTRAINT PK_BookDetails PRIMARY KEY CLUSTERED (BookId ASC),
    CONSTRAINT FK_BookDetails_Books_BookId FOREIGN KEY (BookId) REFERENCES dbo.Books(BookId) ON DELETE CASCADE
);
GO

-- BookAuthors: M:N між Books і Authors, із власним полем Role
IF OBJECT_ID('dbo.BookAuthors', 'U') IS NULL
CREATE TABLE dbo.BookAuthors(
    BookId   BIGINT NOT NULL,
    AuthorId BIGINT NOT NULL,
    Role     NVARCHAR(MAX) NOT NULL,
    CONSTRAINT PK_BookAuthors PRIMARY KEY CLUSTERED (BookId ASC, AuthorId ASC),
    CONSTRAINT FK_BookAuthors_Books_BookId FOREIGN KEY (BookId) REFERENCES dbo.Books(BookId) ON DELETE CASCADE,
    CONSTRAINT FK_BookAuthors_Authors_AuthorId FOREIGN KEY (AuthorId) REFERENCES dbo.Authors(AuthorId) ON DELETE CASCADE
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BookAuthors_AuthorId')
    CREATE NONCLUSTERED INDEX IX_BookAuthors_AuthorId ON dbo.BookAuthors(AuthorId ASC);
GO

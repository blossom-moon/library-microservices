IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
CREATE TABLE [Authors] (
    [AuthorId] bigint NOT NULL IDENTITY,
    [FullName] nvarchar(200) NOT NULL,
    CONSTRAINT [PK_Authors] PRIMARY KEY ([AuthorId])
);

CREATE TABLE [Categories] (
    [CategoryId] bigint NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    CONSTRAINT [PK_Categories] PRIMARY KEY ([CategoryId])
);

CREATE TABLE [Publishers] (
    [PublisherId] bigint NOT NULL IDENTITY,
    [Name] nvarchar(200) NOT NULL,
    [Country] nvarchar(100) NOT NULL,
    CONSTRAINT [PK_Publishers] PRIMARY KEY ([PublisherId])
);

CREATE TABLE [Books] (
    [BookId] bigint NOT NULL IDENTITY,
    [Isbn] nvarchar(20) NOT NULL,
    [Title] nvarchar(300) NOT NULL,
    [Price] decimal(18,2) NOT NULL,
    [AvailableCopies] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    [PublisherId] bigint NOT NULL,
    [CategoryId] bigint NOT NULL,
    CONSTRAINT [PK_Books] PRIMARY KEY ([BookId]),
    CONSTRAINT [CK_Books_AvailableCopies] CHECK ([AvailableCopies] >= 0),
    CONSTRAINT [CK_Books_Price] CHECK ([Price] > 0),
    CONSTRAINT [FK_Books_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([CategoryId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Books_Publishers_PublisherId] FOREIGN KEY ([PublisherId]) REFERENCES [Publishers] ([PublisherId]) ON DELETE NO ACTION
);

CREATE TABLE [BookAuthors] (
    [BookId] bigint NOT NULL,
    [AuthorId] bigint NOT NULL,
    [Role] nvarchar(50) NOT NULL,
    [AddedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_BookAuthors] PRIMARY KEY ([BookId], [AuthorId]),
    CONSTRAINT [FK_BookAuthors_Authors_AuthorId] FOREIGN KEY ([AuthorId]) REFERENCES [Authors] ([AuthorId]) ON DELETE CASCADE,
    CONSTRAINT [FK_BookAuthors_Books_BookId] FOREIGN KEY ([BookId]) REFERENCES [Books] ([BookId]) ON DELETE CASCADE
);

CREATE TABLE [BookDetails] (
    [BookId] bigint NOT NULL,
    [Annotation] nvarchar(2000) NOT NULL,
    [PageCount] int NOT NULL,
    [Language] nvarchar(10) NOT NULL,
    CONSTRAINT [PK_BookDetails] PRIMARY KEY ([BookId]),
    CONSTRAINT [CK_BookDetails_PageCount] CHECK ([PageCount] > 0),
    CONSTRAINT [FK_BookDetails_Books_BookId] FOREIGN KEY ([BookId]) REFERENCES [Books] ([BookId]) ON DELETE CASCADE
);

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'CategoryId', N'Name') AND [object_id] = OBJECT_ID(N'[Categories]'))
    SET IDENTITY_INSERT [Categories] ON;
INSERT INTO [Categories] ([CategoryId], [Name])
VALUES (CAST(1 AS bigint), N'Наукова фантастика'),
(CAST(2 AS bigint), N'Історичний роман'),
(CAST(3 AS bigint), N'Поезія'),
(CAST(4 AS bigint), N'Класична проза');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'CategoryId', N'Name') AND [object_id] = OBJECT_ID(N'[Categories]'))
    SET IDENTITY_INSERT [Categories] OFF;

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'PublisherId', N'Country', N'Name') AND [object_id] = OBJECT_ID(N'[Publishers]'))
    SET IDENTITY_INSERT [Publishers] ON;
INSERT INTO [Publishers] ([PublisherId], [Country], [Name])
VALUES (CAST(1 AS bigint), N'Ukraine', N'Видавництво Старого Лева'),
(CAST(2 AS bigint), N'Ukraine', N'А-БА-БА-ГА-ЛА-МА-ГА'),
(CAST(3 AS bigint), N'Ukraine', N'Фоліо');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'PublisherId', N'Country', N'Name') AND [object_id] = OBJECT_ID(N'[Publishers]'))
    SET IDENTITY_INSERT [Publishers] OFF;

CREATE INDEX [IX_Authors_FullName] ON [Authors] ([FullName]);

CREATE INDEX [IX_BookAuthors_AuthorId] ON [BookAuthors] ([AuthorId]);

CREATE INDEX [IX_BookAuthors_AuthorId_AddedAt] ON [BookAuthors] ([AuthorId], [AddedAt]);

CREATE INDEX [IX_Books_CategoryId] ON [Books] ([CategoryId]);

CREATE UNIQUE INDEX [IX_Books_Isbn] ON [Books] ([Isbn]);

CREATE INDEX [IX_Books_PublisherId] ON [Books] ([PublisherId]);

CREATE INDEX [IX_Books_Title] ON [Books] ([Title]);

CREATE UNIQUE INDEX [IX_Categories_Name] ON [Categories] ([Name]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261111144329_InitialCreate', N'10.0.12');

COMMIT;
GO


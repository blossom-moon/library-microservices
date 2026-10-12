/*
    Library_Catalog — ідемпотентні тестові дані (Проєкт №2).
    Виконати ПІСЛЯ schema.sql. Можна запускати повторно.

    Порядок вставки Books узгоджений з db/p1/seed.sql: якщо Books тут порожня
    перед першим запуском, IDENTITY видасть ті самі BookId (1-4), що й у
    Library_Loans — так підтримується політика дублювання (Catalog — джерело
    правди для книги, Loans зберігає локальну копію BookId/Isbn/Title).
*/

USE Library_Catalog;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Publishers WHERE Name = N'Дніпро')
    INSERT INTO dbo.Publishers (Name, Country) VALUES (N'Дніпро', N'Україна');

IF NOT EXISTS (SELECT 1 FROM dbo.Categories WHERE Name = N'Класична проза')
    INSERT INTO dbo.Categories (Name) VALUES (N'Класична проза');
IF NOT EXISTS (SELECT 1 FROM dbo.Categories WHERE Name = N'Поезія')
    INSERT INTO dbo.Categories (Name) VALUES (N'Поезія');

IF NOT EXISTS (SELECT 1 FROM dbo.Authors WHERE FullName = N'Тарас Шевченко')
    INSERT INTO dbo.Authors (FullName) VALUES (N'Тарас Шевченко');
IF NOT EXISTS (SELECT 1 FROM dbo.Authors WHERE FullName = N'Іван Багряний')
    INSERT INTO dbo.Authors (FullName) VALUES (N'Іван Багряний');
IF NOT EXISTS (SELECT 1 FROM dbo.Authors WHERE FullName = N'Валер''ян Підмогильний')
    INSERT INTO dbo.Authors (FullName) VALUES (N'Валер''ян Підмогильний');
IF NOT EXISTS (SELECT 1 FROM dbo.Authors WHERE FullName = N'Панас Мирний')
    INSERT INTO dbo.Authors (FullName) VALUES (N'Панас Мирний');
GO

DECLARE @PublisherId BIGINT = (SELECT PublisherId FROM dbo.Publishers WHERE Name = N'Дніпро');
DECLARE @CatPoetry BIGINT = (SELECT CategoryId FROM dbo.Categories WHERE Name = N'Поезія');
DECLARE @CatProse BIGINT = (SELECT CategoryId FROM dbo.Categories WHERE Name = N'Класична проза');

IF NOT EXISTS (SELECT 1 FROM dbo.Books WHERE Isbn = '978-966-03-1234-5')
    INSERT INTO dbo.Books (Isbn, Title, Price, PublisherId, CategoryId)
    VALUES ('978-966-03-1234-5', N'Кобзар', 250.00, @PublisherId, @CatPoetry);

IF NOT EXISTS (SELECT 1 FROM dbo.Books WHERE Isbn = '978-966-03-5678-9')
    INSERT INTO dbo.Books (Isbn, Title, Price, PublisherId, CategoryId)
    VALUES ('978-966-03-5678-9', N'Тигролови', 220.00, @PublisherId, @CatProse);

IF NOT EXISTS (SELECT 1 FROM dbo.Books WHERE Isbn = '978-617-12-9876-5')
    INSERT INTO dbo.Books (Isbn, Title, Price, PublisherId, CategoryId)
    VALUES ('978-617-12-9876-5', N'Місто', 210.00, @PublisherId, @CatProse);

IF NOT EXISTS (SELECT 1 FROM dbo.Books WHERE Isbn = '978-617-12-4321-0')
    INSERT INTO dbo.Books (Isbn, Title, Price, PublisherId, CategoryId)
    VALUES ('978-617-12-4321-0', N'Хіба ревуть воли, як ясла повні?', 240.00, @PublisherId, @CatProse);
GO

-- BookDetails (1:1)
INSERT INTO dbo.BookDetails (BookId, Annotation, PageCount, Language)
SELECT b.BookId, N'Класичний твір української літератури.', 320, N'Українська'
FROM dbo.Books b
WHERE NOT EXISTS (SELECT 1 FROM dbo.BookDetails d WHERE d.BookId = b.BookId);
GO

-- BookAuthors (M:N, власне поле Role)
INSERT INTO dbo.BookAuthors (BookId, AuthorId, Role)
SELECT b.BookId, a.AuthorId, N'Автор'
FROM dbo.Books b
JOIN dbo.Authors a ON
    (b.Isbn = '978-966-03-1234-5' AND a.FullName = N'Тарас Шевченко') OR
    (b.Isbn = '978-966-03-5678-9' AND a.FullName = N'Іван Багряний') OR
    (b.Isbn = '978-617-12-9876-5' AND a.FullName = N'Валер''ян Підмогильний') OR
    (b.Isbn = '978-617-12-4321-0' AND a.FullName = N'Панас Мирний')
WHERE NOT EXISTS (SELECT 1 FROM dbo.BookAuthors ba WHERE ba.BookId = b.BookId AND ba.AuthorId = a.AuthorId);
GO

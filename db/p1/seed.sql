/*
    Library_Loans — ідемпотентні тестові дані (Проєкт №1).
    Виконати ПІСЛЯ schema.sql і procedures.sql. Можна запускати повторно —
    кожен INSERT захищений IF NOT EXISTS за природним ключем.

    BookId тут узгоджений з db/p2/seed.sql (Library.Catalog) — обидва сервіси
    посилаються на ті самі BookId/Isbn, це і є "політика дублювання" даних
    про книги між Catalog (джерело правди) і Loans (локальна копія).
*/

USE Library_Loans;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Readers WHERE CardNumber = 'RDR-0001' OR Email = 'kostetska.kristina2005@gmail.com')
    INSERT INTO dbo.Readers (CardNumber, FullName, Email, Phone)
    VALUES ('RDR-0001', N'Христина Костецька', 'kostetska.kristina2005@gmail.com', '+380000000001');

IF NOT EXISTS (SELECT 1 FROM dbo.Readers WHERE CardNumber = 'RDR-0002' OR Email = 'ivan.petrenko@example.com')
    INSERT INTO dbo.Readers (CardNumber, FullName, Email, Phone)
    VALUES ('RDR-0002', N'Іван Петренко', 'ivan.petrenko@example.com', '+380000000002');

IF NOT EXISTS (SELECT 1 FROM dbo.Readers WHERE CardNumber = 'RDR-0003' OR Email = 'olena.koval@example.com')
    INSERT INTO dbo.Readers (CardNumber, FullName, Email, Phone)
    VALUES ('RDR-0003', N'Олена Коваль', 'olena.koval@example.com', NULL);

-- BookId у Books не є IDENTITY (значення приходять з Library.Catalog), тому
-- вставляємо їх явно без SET IDENTITY_INSERT.
IF NOT EXISTS (SELECT 1 FROM dbo.Books WHERE BookId = 1)
    INSERT INTO dbo.Books (BookId, Isbn, Title, IsAvailable)
    VALUES (1, '978-966-03-1234-5', N'Кобзар', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Books WHERE BookId = 2)
    INSERT INTO dbo.Books (BookId, Isbn, Title, IsAvailable)
    VALUES (2, '978-966-03-5678-9', N'Тигролови', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Books WHERE BookId = 3)
    INSERT INTO dbo.Books (BookId, Isbn, Title, IsAvailable)
    VALUES (3, '978-617-12-9876-5', N'Місто', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Books WHERE BookId = 4)
    INSERT INTO dbo.Books (BookId, Isbn, Title, IsAvailable)
    VALUES (4, '978-617-12-4321-0', N'Хіба ревуть воли, як ясла повні?', 1);
GO

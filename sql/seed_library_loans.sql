/*
    Ідемпотентний seed-скрипт для Library_Loans (Lab01, крок 8: повторний
    запуск не створює дублікатів). Кожен INSERT обгорнутий у
    IF NOT EXISTS (...) за природним унікальним ключем сутності
    (CardNumber для читачів, Isbn для книг) — а не за IDENTITY-значенням,
    яке на повторному запуску не допоможе.

    Виконати в SSMS в контексті бази Library_Loans, ПІСЛЯ schema.sql
    і procedures.sql (включно з fix_sp_ReturnBook_sp_CancelLoan.sql).
*/

USE Library_Loans;
GO

-- Readers -----------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.Readers WHERE CardNumber = 'RDR-0001')
    INSERT INTO dbo.Readers (CardNumber, FullName, Email, Phone)
    VALUES ('RDR-0001', N'Христина Костецька', 'kostetska.kristina2005@gmail.com', '+380000000001');

IF NOT EXISTS (SELECT 1 FROM dbo.Readers WHERE CardNumber = 'RDR-0002')
    INSERT INTO dbo.Readers (CardNumber, FullName, Email, Phone)
    VALUES ('RDR-0002', N'Іван Петренко', 'ivan.petrenko@example.com', '+380000000002');

IF NOT EXISTS (SELECT 1 FROM dbo.Readers WHERE CardNumber = 'RDR-0003')
    INSERT INTO dbo.Readers (CardNumber, FullName, Email, Phone)
    VALUES ('RDR-0003', N'Олена Коваль', 'olena.koval@example.com', NULL);

-- Books ---------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.Books WHERE Isbn = '978-966-03-1234-5')
    INSERT INTO dbo.Books (Isbn, Title, Author, IsAvailable)
    VALUES ('978-966-03-1234-5', N'Кобзар', N'Тарас Шевченко', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Books WHERE Isbn = '978-966-03-5678-9')
    INSERT INTO dbo.Books (Isbn, Title, Author, IsAvailable)
    VALUES ('978-966-03-5678-9', N'Тигролови', N'Іван Багряний', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Books WHERE Isbn = '978-617-12-9876-5')
    INSERT INTO dbo.Books (Isbn, Title, Author, IsAvailable)
    VALUES ('978-617-12-9876-5', N'Місто', N'Валер''ян Підмогильний', 1);

IF NOT EXISTS (SELECT 1 FROM dbo.Books WHERE Isbn = '978-617-12-4321-0')
    INSERT INTO dbo.Books (Isbn, Title, Author, IsAvailable)
    VALUES ('978-617-12-4321-0', N'Хіба ревуть воли, як ясла повні?', N'Панас Мирний', 1);

GO

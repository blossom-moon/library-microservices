# LibraryLabs

Три сервіси бібліотечної системи, кожен зі своєю базою (database-per-service):

| Сервіс | Домен | Технологія | Папка |
|---|---|---|---|
| **Library.Loans** | Читачі, Книги, Видачі | SQL Server, ADO.NET + Dapper, 3-шарова архітектура | `Library.Loans.Solution/` |
| **Library.Catalog** | Каталог книг (автори, видавці, категорії) | SQL Server, EF Core Code-First | `Library.Catalog/` |
| **Library.Reviews** | Відгуки на книги | MongoDB | `Library.Reviews/` |

Між сервісами немає FOREIGN KEY-зв'язків — лише дублювання узгоджених
полів (eventual consistency). Політика дублювання:

| Поле | Де зберігається | Джерело правди (owner) | Політика узгодженості |
|---|---|---|---|
| `BookTitle` | `Library_Loans.dbo.LoanItems.BookTitle` | `Library.Catalog` (`Book.Title`) | Копіюється в момент видачі (`sp_CreateLoan`); не оновлюється ретроактивно, якщо назву пізніше змінили в каталозі — видача відображає назву "на момент видачі" |
| `BookId` / `Isbn` | `Library_Loans.dbo.Books`, `Library.Catalog.Books`, `Library.Reviews` (`bookId`/`isbn` у документі відгуку) | `Library.Catalog` | `Isbn` як природний ключ синхронізації; `Library.Loans` веде власний `IsAvailable`, що є локальним станом (не каталожним полем) |
| `ReaderFullName` (якщо використовується у відгуках) | `Library.Reviews` (`authorName`) | `Library.Loans.dbo.Readers.FullName` | Копіюється при створенні відгуку, не синхронізується назад |

## 1. Розгортання Lab01 (Library.Loans — SQL Server)

Виконати в SSMS, **у порядку**, у контексті бази `Library_Loans`:

1. `schema.sql` — таблиці, індекси, обмеження (у вас уже застосовано раніше).
2. `sql/fix_sp_ReturnBook_sp_CancelLoan.sql` — оновлені процедури з явною
   транзакцією, `UPDLOCK`-перевіркою статусу та `THROW` на неприпустимий
   перехід (50001/50002/50003). **Обов'язково перевиконати** — без цього
   кроку обробка помилок у `LoanRepository.cs` не працюватиме.
3. `sql/seed_library_loans.sql` — ідемпотентні тестові дані (`IF NOT EXISTS`
   за природним ключем: `CardNumber`, `Isbn`) — можна запускати повторно
   без дублікатів.

Аналогічно для `Library.Catalog` (EF Core, `dotnet ef database update`) і
`Library.Reviews` (MongoDB Atlas, колекції створюються при першому записі).

## 2. Lab02 — тришарова архітектура (Library.Loans.Solution)

```
Library.Loans.Api → Library.Loans.Bll → Library.Loans.Dal → Library.Loans.Domain
```

Запуск без Aspire (поточний спосіб):

```
cd Library.Loans.Solution/Library.Loans.Api
dotnet build
dotnet bin/Debug/net10.0/Library.Loans.Api.dll
```

Відкрити Swagger на показаній у консолі адресі (`https://localhost:XXXX/swagger`).

### Приклади запитів

Створити читача:
```
POST /api/Readers
{
  "cardNumber": "RDR-0010",
  "fullName": "Тест Тестович",
  "email": "test@example.com",
  "phone": "+380000000000"
}
```

Створити видачу:
```
POST /api/Loans
{
  "readerId": 1,
  "dueDate": "2026-11-01",
  "bookIds": [1, 2]
}
```

Повернути книгу:
```
POST /api/Loans/{loanId}/return/{bookId}
```

Скасувати видачу:
```
POST /api/Loans/{loanId}/cancel
```

Отримати видачу з позиціями (multi-mapping JOIN):
```
GET /api/Loans/{loanId}
```

### Запуск через .NET Aspire

Якщо встановлено Aspire workload:

```
dotnet workload install aspire
dotnet new install Aspire.ProjectTemplates
dotnet new aspire-apphost -n Library.Loans.AppHost -o Library.Loans.Solution/Library.Loans.AppHost
```

У створеному `Library.Loans.AppHost/Program.cs`:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var loansDb = builder.AddConnectionString("LibraryLoans");

builder.AddProject<Projects.Library_Loans_Api>("loans-api")
       .WithReference(loansDb);

builder.Build().Run();
```

Додати `Library.Loans.AppHost` до `Library.Loans.slnx`, зробити його
startup-проєктом, запустити — відкриється Aspire Dashboard з логами й
трасуванням `loans-api`.

## 3. Git

Робоча гілкова модель: `main` (стабільний стан) → `develop` (інтеграція) →
`feature/xxx` (кожна фіча окремою гілкою, merge в `develop` через PR).

```
git init
git checkout -b develop
git checkout -b feature/loans-three-tier
# ... коміти ...
git checkout develop && git merge feature/loans-three-tier
```

## 4. Що виправлено в цій ревізії (DAL/BLL, Lab02)

- `IGenericRepository<T>` + `RepositoryBase` + `BaseDapperRepository<T>` —
  спільний CRUD для Dapper-репозиторіїв написано один раз.
- `ReaderRepository` свідомо **не** реалізує `IGenericRepository<T>` —
  демонструє керування `SqlCommand`/`SqlDataReader` вручну.
- `ILoanRepository.GetWithItemsAsync` — multi-mapping (`QueryAsync<Loan,
  LoanItem, Loan>` зі `splitOn`) замість двох окремих запитів.
- `UnitOfWork` створює репозиторії один раз і передає їм делегат
  `Func<SqlTransaction?>` на поточну транзакцію — репозиторій завжди бачить
  актуальний стан навіть після Commit/Rollback.
- `BeginTransactionAsync(IsolationLevel, ct)` — явний рівень ізоляції
  (`ReadCommitted`), обґрунтування — в коментарі в `UnitOfWork.cs`.
- `CreateLoanDto`/`CreateReaderDto` — базова модельна валідація через
  `DataAnnotations` (`[Required]`, `[Range]`, `[EmailAddress]` тощо);
  `[ApiController]` повертає 400 ще до виклику сервіса.
- `sp_ReturnBook`/`sp_CancelLoan` — явна транзакція, `UPDLOCK`-перевірка
  статусу, `THROW` власних кодів помилок (50001/50002/50003), які
  `LoanRepository` перехоплює й перетворює на `NotFoundException`/
  `BusinessConflictException`.
- Ідемпотентний seed-скрипт.

**Не перевірено локальною збіркою** (у цій хмарній сесії немає .NET SDK) —
після заміни файлів обов'язково виконати `dotnet build` і перевірити, що
все компілюється.

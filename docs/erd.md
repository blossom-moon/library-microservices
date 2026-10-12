# ERD та опис контекстів

Предметна область — бібліотека. Система розбита на три незалежні bounded
contexts (кожен — окремий сервіс зі своєю базою; FOREIGN KEY між базами немає).

## 1. Loans (Library_Loans, SQL Server) — видачі

Відповідає за читачів і видачі книг. Книги тут — лише локальна копія
(BookId/Isbn/Title) з Catalog, плюс локальний стан `IsAvailable`.

```mermaid
erDiagram
    Readers ||--o{ Loans : "1:N"
    Loans ||--o| LoanDetails : "1:1"
    Loans ||--o{ LoanItems : "1:N"
    Books ||--o{ LoanItems : "1:N (M:N Loans-Books)"
    Loans ||--o{ LoanStatusHistory : "1:N"

    Readers {
        bigint ReaderId PK
        nvarchar CardNumber UK
        nvarchar FullName
        nvarchar Email UK
        nvarchar Phone
        bit IsDeleted
        datetime2 CreatedAt
        nvarchar CreatedBy
        datetime2 UpdatedAt
        nvarchar UpdatedBy
    }
    Loans {
        bigint LoanId PK
        bigint ReaderId FK
        nvarchar Status
        date LoanDate
        date DueDate
        date ReturnDate
        decimal FineAmount
        bit IsDeleted
        rowversion RowVer
    }
    LoanDetails {
        bigint LoanId PK, FK
        nvarchar PickupPoint
        nvarchar Comment
    }
    Books {
        bigint BookId PK
        nvarchar Isbn UK
        nvarchar Title
        bit IsAvailable
        datetime2 SyncedAt
    }
    LoanItems {
        bigint LoanItemId PK
        bigint LoanId FK
        bigint BookId FK
        nvarchar BookTitle
        datetime2 ReturnedAt
    }
    LoanStatusHistory {
        bigint HistoryId PK
        bigint LoanId FK
        nvarchar OldStatus
        nvarchar NewStatus
        datetime2 ChangedAt
        nvarchar ChangedBy
    }
```

Зв'язки: Readers–Loans 1:N; Loans–LoanDetails 1:1; Loans–Books M:N через
LoanItems (власні поля `BookTitle`, `ReturnedAt`); Loans–LoanStatusHistory 1:N.

## 2. Catalog (Library_Catalog, SQL Server + EF Core) — каталог

Джерело правди про книги, авторів, видавців і категорії.

```mermaid
erDiagram
    Publishers ||--o{ Books : "1:N"
    Categories ||--o{ Books : "1:N"
    Books ||--o| BookDetails : "1:1"
    Books ||--o{ BookAuthors : "1:N"
    Authors ||--o{ BookAuthors : "1:N (M:N Books-Authors)"

    Publishers { bigint PublisherId PK
        nvarchar Name
        nvarchar Country }
    Categories { bigint CategoryId PK
        nvarchar Name UK }
    Authors { bigint AuthorId PK
        nvarchar FullName }
    Books {
        bigint BookId PK
        nvarchar Isbn UK
        nvarchar Title
        decimal Price
        bigint PublisherId FK
        bigint CategoryId FK
        bit IsDeleted
        rowversion RowVersion
    }
    BookDetails {
        bigint BookId PK, FK
        nvarchar Annotation
        int PageCount
        nvarchar Language
    }
    BookAuthors {
        bigint BookId PK, FK
        bigint AuthorId PK, FK
        nvarchar Role
    }
```

Зв'язки: Publishers–Books 1:N; Categories–Books 1:N; Books–BookDetails 1:1;
Books–Authors M:N через BookAuthors (власне поле `Role`).

## 3. Reviews (Library_Reviews, MongoDB) — відгуки й обговорення

| Колекція | Підхід | Опис |
|---|---|---|
| `discussions` | referencing | `messageIds` — масив ObjectId-посилань на документи `messages` |
| `messages` | — | окремі повідомлення, на які посилаються обговорення (`authorId`, `text`) |
| `reviews` | гібридний + embedding | `bookId`/`readerId` — посилання, `readerName` продубльовано; у деяких документах вкладені масиви `comments`, `tags` |
| `ratings` | гібридний знімок | агрегат на книгу (`bookId`, продубльована `bookTitle`, `avgRating`, `totalReviews`) |

Гнучка схема: документи в `reviews` мають різні набори полів (`photoUrl`,
`verifiedPurchase`, `comments`, `tags` — лише там, де потрібні).

Приклад документа `reviews`:

```json
{
  "bookId": 103,
  "readerId": 90,
  "readerName": "Ірина Бойко",
  "rating": 4,
  "text": "Гарне видання, ілюстрації чудові.",
  "createdAt": "2026-09-24T16:44:56.966Z",
  "photoUrl": "https://example.com/photos/book103.jpg",
  "verifiedPurchase": true
}
```

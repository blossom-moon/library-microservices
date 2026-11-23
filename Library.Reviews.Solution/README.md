# Library.Reviews — мікросервіс відгуків (Lab04)

Clean Architecture · CQRS · MediatR · Value Objects · Pipeline Behaviors · MongoDB (Atlas).

## Шари та напрямок залежностей

```
┌──────────────────────────────────────────────┐
│ Library.Reviews.WebApi                       │  контролери (лише ISender), AutoMapper,
│                                              │  Swagger, ProblemDetails, Serilog
└───────────────┬───────────────┬──────────────┘
                │               │
                ▼               ▼
┌───────────────────────┐   ┌────────────────────────────────┐
│ Library.Reviews       │   │ Library.Reviews.Infrastructure │  MongoDbContext, BsonClassMap,
│ .Application          │◄──│                                │  репозиторій, запити, індекси,
│ CQRS, MediatR,        │   │                                │  seeding, health check
│ Behaviors, валідація  │   └──────────────┬─────────────────┘
└───────────┬───────────┘                  │
            ▼                              ▼
┌──────────────────────────────────────────────┐
│ Library.Reviews.Domain                       │  сутності, Value Objects, винятки,
│ (не залежить ні від кого)                    │  IReviewRepository
└──────────────────────────────────────────────┘
```

* Domain → нічого (лише `MongoDB.Bson` заради `ObjectId`; `MongoDB.Driver` відсутній).
* Application → Domain. Infrastructure → Application + Domain. WebApi → Application + Infrastructure.

## Основне

* **Domain:** `BaseEntity` (`ObjectId Id`, `CreatedAt`, `UpdatedAt`, `Version`), сутності `Review` і `Discussion` із приватними сеттерами та вкладеними колекціями (`_comments`, `_messages`), базовий `ValueObject`, незмінні VO `Author`, `Comment`, `DomainException`/`NotFoundException`/`ConflictException`.
* **CQRS:** `CreateReviewCommand`, `UpdateReviewCommand`, `DeleteReviewCommand` (окремі handlers), `GetReviewByIdQuery`, `GetReviewsListQuery` (пагінація). Читання через `IReviewQueries`.
* **Pipeline (порядок виконання):** `LoggingBehavior` → `ValidationBehavior` → `PerformanceBehavior` → handler.
* **Валідація:** FluentValidation, перевірка `ObjectId`, асинхронне правило (запит до MongoDB: один відгук від користувача на книгу), всі помилки збираються разом.
* **MongoDB:** `MongoDbContext` через `IOptions<MongoDbOptions>`, `BsonClassMap` без атрибутів (camelCase), оптимістична конкурентність (`ReplaceOne` за `version`, інакше 409), читання проєкцією `$project` одразу в DTO.
* **Індекси** (створює `IndexCreationService` при старті): `{bookId:1, createdAt:-1}`, текстовий на `text`/`bookTitle`, унікальний `{bookId:1, "author.userId":1}`.
* **Seeding:** `IDataSeeder` + `InsertMany` з перевіркою наявності даних (ідемпотентно); вимикається `Seeding:Enabled=false`.
* **Health check:** `GET /health` (ping MongoDB).

## Коди відповідей

| Операція | Успіх | Помилки |
|---|---|---|
| GET /api/reviews, GET /api/reviews/{id} | 200 | 400 (некоректний ObjectId/параметри), 404 |
| POST /api/reviews | 201 + `Location` із ObjectId | 400 (валідація, Rating=10), 409 (дубль) |
| PUT /api/reviews/{id} (`If-Match: "версія"`) | 200 | 400, 404, 409 (конфлікт версій) |
| DELETE /api/reviews/{id} | 204 | 400, 404 |

MongoDB-помилки: `DuplicateKey` → 409, `MongoConnectionException`/`MongoTimeoutException` → 503.

## Запуск

1. Створіть кластер у MongoDB Atlas і додайте свою IP-адресу в **Network Access**.
2. Рядок підключення **не зберігається в репозиторії** — задайте його секретом:

```powershell
cd Library.Reviews.WebApi
dotnet user-secrets set "MongoDb:ConnectionString" "mongodb+srv://<user>:<password>@<cluster>.mongodb.net/"
```

3. Запуск:

```powershell
dotnet build
dotnet test
dotnet run --project Library.Reviews.WebApi
```

Swagger: http://localhost:5240/swagger · Health: http://localhost:5240/health

## Перевірка в Compass / mongosh

```js
use Library_Reviews_Lab04
db.reviews.find().pretty()
db.reviews.getIndexes()
```

## Перевірка pipeline

`POST /api/reviews` із `rating: 10` → `400 Validation failed`. У логах є `Handling CreateReviewCommand`, але немає `Executing CreateReviewCommandHandler` — обробник не викликався.

# Lab05 — Мікросервісна платформа на .NET Aspire

Три сервіси бібліотеки (Lab02–Lab04) під оркестрацією **.NET Aspire**, єдина точка входу — **YARP Gateway**, композитні відповіді — **Aggregator**, наскрізна діагностика — **OpenTelemetry + Serilog + CorrelationId**.

| Сервіс (логічне ім'я) | Звідки | Дані |
|---|---|---|
| `loans-api` | Lab02 (читачі, видачі) | SQL Server `LibraryLoans`, Dapper |
| `catalog-api` | Lab03 (книги, автори, категорії) | SQL Server `CatalogDb`, EF Core |
| `reviews-api` | Lab04 (відгуки, Clean Architecture/CQRS) | MongoDB `ReviewsDb` |
| `aggregator` | нове | викликає три сервіси |
| `gateway` | нове | YARP, **http://localhost:5000** |

> У завданні сервіс-приклад названий `orders`; у нашому домені його роль виконує `loans` (видачі).

```
Клієнт ──► gateway :5000 ──► /api/loans|readers   → loans-api   → SQL Server
                        ├──► /api/books|authors|categories → catalog-api → SQL Server
                        ├──► /api/reviews          → reviews-api → MongoDB
                        └──► /api/aggregator/**    → aggregator ─► loans-api + catalog-api ∥ reviews-api
```

## Структура
```
Platform.AppHost          оркестрація (посилається на всі проєкти)
Platform.ServiceDefaults  Serilog, OpenTelemetry, health checks, service discovery, resilience, CorrelationId
Platform.ApiGateway       YARP + логування маршрутів
Platform.Aggregator       типізовані клієнти + Task.WhenAll + часткові відмови
Services/Loans|Catalog|Reviews   сервіси (змінено лише Program.cs та логування бізнес-подій)
```
Залежності: AppHost → усі; сервіси/Gateway/Aggregator → лише ServiceDefaults; на AppHost не посилається ніхто.

## Запуск
Потрібно: .NET 10 SDK, **Docker Desktop** (запущений).

1. Пароль SQL Server як секрет (в репозиторій не потрапляє; мінімум 8 символів, великі/малі літери, цифра, спецсимвол):
   ```powershell
   cd Platform.AppHost
   dotnet user-secrets set "Parameters:sql-password" "<ваш_пароль>"
   ```
2. Міграції Catalog (Lab03) мають лежати в `Services/Catalog/Library.Catalog.Dal/Migrations` (сервіс застосовує їх при старті). Якщо їх нема:
   ```powershell
   dotnet ef migrations add InitialCreate --project Services/Catalog/Library.Catalog.Dal --startup-project Services/Catalog/Library.Catalog.Api
   ```
3. Запуск:
   ```powershell
   dotnet run --project Platform.AppHost -p:UseAppHost=false
   ```
   Aspire Dashboard відкриється сам (ресурси, логи, **traces**, метрики). Порядок старту: БД → сервіси → aggregator → gateway.

Gateway: **http://localhost:5000**. Усі запити — лише через нього.

## Приклади запитів
```powershell
# читач 1 і видача книг 1 та 3 (Loans)
curl -X POST http://localhost:5000/api/loans -H "Content-Type: application/json" -d '{"readerId":1,"dueDate":"2030-01-01T00:00:00","bookIds":[1,3]}'
curl http://localhost:5000/api/loans/1

# каталог
curl http://localhost:5000/api/books/1

# відгуки (Mongo)
curl -X POST http://localhost:5000/api/reviews -H "Content-Type: application/json" -d '{"bookId":1,"bookTitle":"Кобзар","authorId":"u1","authorName":"Христина","rating":5,"text":"Чудова книга, раджу!"}'
curl "http://localhost:5000/api/reviews?bookId=1"

# агрегований запит: видача + книги з каталогу + відгуки
curl -i http://localhost:5000/api/aggregator/loans/1/full -H "X-Correlation-Id: demo-123"
```
Відповідь Gateway містить заголовок `X-Correlation-Id` (той самий, що переданий, або згенерований).

### Часткова відмова
Зупиніть `reviews-api` у Dashboard і повторіть `.../aggregator/loans/1/full`: статус **200**, у відповіді дані видачі й каталогу та `"warnings": ["reviews-api is unavailable"]`. Якщо недоступний `loans-api` — **503**, якщо видачі немає — **404**.

## Спостережуваність
* **Serilog** (ServiceDefaults): compact JSON у консоль, збагачувачі ServiceName/Environment/MachineName/ThreadId, `FromLogContext`, рівень за середовищем, `writeToProviders: true` (логи також йдуть в OpenTelemetry → Dashboard).
* **CorrelationId**: middleware (читає/генерує `X-Correlation-Id`, кладе в `LogContext` та тег Activity) + `DelegatingHandler` для вихідних викликів.
* **OpenTelemetry**: трейси ASP.NET Core, HttpClient, SqlClient, MongoDB + власні `ActivitySource` (`Library.Loans`, `Library.Catalog`, `Library.Reviews`, `Platform.Aggregator`); експорт по OTLP (Aspire виставляє `OTEL_EXPORTER_OTLP_ENDPOINT`).
* Gateway логує метод, шлях, маршрут, кластер, статус, тривалість; помилки проксі — окремим Warning.
* Перевірка: у Dashboard → Traces знайдіть запит `/api/aggregator/loans/1/full` — один trace охоплює gateway → aggregator → loans/catalog/reviews → SQL/Mongo.

## Ключові рішення
* YARP: catch-all маршрути на домен, окремий маршрут `aggregator` з `Order: 1`, адреси кластерів — логічні (`http://loans-api`), розв'язуються через `AddServiceDiscoveryDestinationResolver()`.
* Aggregator: `Task.WhenAll` для незалежних викликів (catalog ∥ reviews), `CancellationToken`, жодних `.Result`/`WaitAll`; кожен виклик логується з тривалістю; збій необов'язкового сервісу → `warnings`, а не 500.
* DTO: camelCase, `null`-поля не серіалізуються.
* Секрети: пароль SQL — `Parameters:sql-password` (user-secrets); рядки підключення сервісам передає AppHost.

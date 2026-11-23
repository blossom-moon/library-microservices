# Lab09 — Аутентифікація та авторизація: Identity + JWT, IdentityServer, Keycloak + Pulumi

Платформа Lab08 захищена трьома незалежними способами; усі сервіси приймають токени кожного з них (policy scheme `Smart` у `Platform.Security` обирає схему за claim `iss`).

| Частина | Що | Де |
|---|---|---|
| 1. Identity + JWT | `Library.Auth.Api` (:5244): реєстрація, підтвердження email, login, refresh/revoke, скидання пароля, користувачі й ролі `User`/`Admin` | `Services/Auth/Library.Auth.Api` |
| 2. IdentityServer | Duende IdentityServer (:5400): Authorization Code + PKCE, Client Credentials, ASP.NET Identity, EF Configuration/Operational store | `Services/IdentityServer/Library.IdentityServer` |
| 3. Keycloak + Pulumi | realm `LibraryRealm`, ресурс-сервери, ролі, політики, клієнти й користувачі як код | `Pulumi/Library.Keycloak.Pulumi` |
| Спільне | `[RequirePermission]`, динамічні політики, мульти-емітентна автентифікація, Client Credentials handler | `Platform.Security` (+ `Platform.Security.Tests`) |
| Заглушка | `payments-api` (:5242): `payments:read/process/refund` | `Services/Payments/Library.Payments.Api` |

## Що де
| Вимога | Реалізація |
|---|---|
| Окрема БД Identity, ролі `User`/`Admin`, seed адміністратора | `AddDbContext<ApplicationDbContext>` + `IdentitySeeder` (Development: `Seed:*` в `appsettings.Development.json`) |
| Пароль ≥ 8, lockout 5 спроб, унікальний email | `Library.Auth.Api/Program.cs` — `AddIdentity(options => …)` |
| Access JWT 15 хв (HS256, claims `sub`, `unique_name`, `email`, `jti`, `role`) | `Services/JwtTokenService.cs` |
| Refresh-токен: 7 днів, у БД лише SHA-256 хеш, rotation, reuse detection, HttpOnly cookie | `Services/RefreshTokenService.cs`, `AuthController.SetRefreshCookie` |
| Підтвердження email, forgot/reset/change password (одноразові токени з терміном дії) | `AuthController` + `IAuthEmailSender` (mock: лист у лог; у Development токен ще й у відповіді) |
| Owner-or-admin, Admin-only ендпоінти | `UsersController` |
| Дозволи `domain:action` на кожному REST-ендпоінті | `[RequirePermission(Permissions.Loans.Read)]` тощо у контролерах Loans/Catalog/Payments |
| Scope-політики для gRPC (M2M) | `[Authorize(Policy = ApiScopes.LoansRead)]` на gRPC-сервісах |
| Client Credentials в Aggregator | `Platform.Security/ClientCredentials` + `.AddHttpMessageHandler<ClientCredentialsTokenHandler>()` на gRPC/HTTP-клієнтах |
| Gateway: перший рубіж, публічні/захищені маршрути, `X-User-Id` | `Platform.ApiGateway/Program.cs`, `appsettings.json` (`auth-public` — anonymous, решта — `authenticated`) |
| IdentityServer: клієнти `postman` (Code+PKCE), `swagger` (public PKCE), `aggregator_service` (Client Credentials) | `Library.IdentityServer/Data/IdentityServerConfig.cs` |
| Keycloak як код | `Pulumi/Library.Keycloak.Pulumi/LibraryStack.cs`, матриця — `Matrix.cs`, клієнти — `ClientExtensions.cs` |

## Запуск
```powershell
# 1. Секрети AppHost (один раз)
cd Platform.AppHost
dotnet user-secrets set "Parameters:sql-password" "<пароль SQL>"
dotnet user-secrets set "Parameters:rabbit-password" "<пароль>"
dotnet user-secrets set "Parameters:jwt-secret" "<випадковий рядок ≥ 32 символи>"
dotnet user-secrets set "Parameters:aggregator-client-secret" "<секрет клієнта aggregator_service>"
dotnet user-secrets set "Parameters:keycloak-admin-password" "<пароль адміна Keycloak>"

# 2. Міграції (потрібно один раз; dotnet-ef: dotnet tool install --global dotnet-ef)
cd ..\Services\Auth\Library.Auth.Api
dotnet ef migrations add Initial
cd ..\..\IdentityServer\Library.IdentityServer
dotnet ef migrations add Initial -c ApplicationDbContext      -o Data/Migrations/Identity
dotnet ef migrations add Initial -c PersistedGrantDbContext   -o Data/Migrations/PersistedGrant
dotnet ef migrations add Initial -c ConfigurationDbContext    -o Data/Migrations/Configuration

# 3. Запуск (копія на C:, якщо WDAC блокує D:)
dotnet run --project ..\..\..\Platform.AppHost -p:UseAppHost=false

# 4. Keycloak: коли ресурс keycloak Healthy
cd ..\..\..\Pulumi\Library.Keycloak.Pulumi
pulumi login --local; pulumi stack init dev
pulumi config set --secret keycloak:password "<keycloak-admin-password>"
pulumi up
pulumi stack output PostmanClientSecret --show-secrets
```
Dev-облікові дані: Auth — `admin` / `Admin#12345` (лише Development); IdentityServer — `alice`, `bob` (пароль `Pass#12345`); Keycloak — `alice` (admin), `bob` (manager), `carol` (user), `dave` (guest), пароль `Pass#12345`.

## Матриця прав
| Дозвіл | admin | manager | user | guest |
|---|:-:|:-:|:-:|:-:|
| `loans:read` / `loans:create` | ✓ | ✓ | ✓ | – |
| `loans:update` (повернення) | ✓ | ✓ | – | – |
| `loans:delete` (скасування) | ✓ | – | – | – |
| `catalog:read` | ✓ | ✓ | ✓ | ✓ |
| `catalog:create` / `catalog:update` | ✓ | ✓ | – | – |
| `catalog:delete` | ✓ | – | – | – |
| `payments:read` | ✓ | ✓ | ✓ | – |
| `payments:process` | ✓ | ✓ | – | – |
| `payments:refund` | ✓ | – | – | – |

`loans:approve` і `catalog:publish` визначені в матриці (Keycloak), але відповідних ендпоінтів у домені немає. Для власних токенів (Identity): `Admin` — усе, `User` — `*:read` і `*:create`; для IdentityServer права визначаються `scope` (`loans.read` ≙ `loans:read`).
Фактичні коди — `./scripts/check-matrix.ps1` (результат у `matrix-result.csv` → вставити в звіт). Без токена будь-який `/api/**` → **401**; токен без потрібного дозволу → **403**.

## Postman
`postman/Library-Lab09.postman_collection.json`: Identity (register → verify → login → refresh → logout), IdentityServer (Authorization Code+PKCE налаштовується на вкладці Authorization; Client Credentials), Keycloak (токени для alice/bob/carol/dave та виклики з різних ролей), перевірка без токена. Змінні: `keycloakPostmanSecret` (вивід Pulumi), `aggregatorSecret` (= `Parameters:aggregator-client-secret`).

## Розшифровані JWT для звіту
Вставити токен на jwt.io (або `[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String(...))`) і зробити скриншот:
- Identity: `role`: `User` / `Admin`, `sub`, `unique_name`, `exp` (+15 хв).
- IdentityServer Client Credentials: `client_id`, `scope`, `aud` = `*_api`, **немає `sub`** (немає користувача).
- Keycloak manager (bob): `resource_access.loans-api.roles` і `catalog-api.roles` зі списком дозволів composite-ролі `manager`.

## Ключові рішення
- **Три емітенти — одна політика дозволів**: `PermissionEvaluator` читає Keycloak (`authorization.permissions` / `resource_access`), `scope` (IdentityServer) або `role` (Identity). Контролери не залежать від провайдера.
- **Динамічні політики**: `RequirePermission("loans:read")` → політика `perm:loans:read` створюється провайдером за потреби; немає ручної реєстрації десятків політик.
- **Gateway і сервіси перевіряють токен обидва** (defense in depth): Gateway відсікає анонімів, сервіс не довіряє мережі.
- **Refresh-токен**: у БД лише хеш; повторне використання вже відкликаного токена = ознака крадіжки → відкликаються всі сесії користувача.
- **Keycloak: `ValidateAudience=false`**: audience Keycloak-токенів — `account`, а не API; без цього замість 403 було б 401. Доступ визначають ролі/дозволи.
- **MapInboundClaims=false**, `RoleClaimType = "role"`: імена claims у коді збігаються з тими, що в токені.

## Питання до захисту
- **Authentication vs Authorization?** Хто ти (токен валідний) vs що тобі дозволено (роль/дозвіл). 401 — немає/невалідний токен; 403 — токен є, прав немає.
- **Чому access токен короткий, а refresh довгий і в HttpOnly cookie?** Викрадений access діє хвилини; HttpOnly cookie недоступна JS (XSS), `Path=/auth` і `SameSite=Strict` обмежують відправлення.
- **Що таке rotation і reuse detection?** Кожен refresh видає новий токен, старий відкликається; повторне пред'явлення старого → відкликання всіх токенів користувача.
- **Authorization Code + PKCE і Client Credentials — коли що?** Code+PKCE — користувач через браузер, `code_verifier` захищає від перехоплення коду. Client Credentials — сервіс–сервіс, без користувача (немає `sub`).
- **Чому секрет клієнта зберігається хешем (`Sha256`)?** У БД IdentityServer не лежить відкритий секрет.
- **Роль vs дозвіл (scope)?** Роль — група користувачів; дозвіл — дія над ресурсом. Composite-роль Keycloak збирає дозволи й розгортається в токені.
- **Навіщо Pulumi?** Конфігурація Keycloak відтворювана, версіонована й перевіряється ревʼю; `pulumi up` = той самий realm на будь-якому стенді.

## Обмеження
- Код не компілювався в середовищі розробки (немає .NET SDK): версії пакетів `Duende.IdentityServer*` (7.*), `Aspire.Hosting.Keycloak` (preview) і API `Pulumi.Keycloak` перевірити під час першої збірки.
- Міграції EF генеруються командами вище (design-time фабрики є).
- Swagger UI не налаштований на OAuth (клієнт `swagger` у IdentityServer і Keycloak створений, але UI не підключений).
- Листи — mock (лог). Outbox, ротація ключів HS256, HTTPS у Development не реалізовано.

---

# Lab08 — Подієва мікросервісна архітектура з RabbitMQ

Розширення платформи з Lab07: **Каталог** публікує події про зміну книги, а **Видачі** (Lab02) і **Відгуки** (Lab04) оновлюють свої локальні копії даних згідно з політикою дублювання з Лаб. 01.

| Подія (Каталог → брокер) | Хто споживає | Що оновлюється |
|---|---|---|
| `BookPriceChangedEvent` (`book.price_changed`) | `loans-api` | `Books.Price` у БД видач; ціни/назви в позиціях `LoanItems` — знімки, **не змінюються** |
| `BookRenamedEvent` (`book.renamed`) | `loans-api`, `reviews-api` | `Books.Title` у видачах; поле `bookTitle` в усіх відгуках про книгу |

```
PUT /api/books/{id}/price|title ─► catalog-api ─ SaveChanges ─► publish ─► exchange catalog-events (topic)
                                                                              ├─ "book.*"       ─► loans.catalog-sync   ─► loans-api   (Books у SQL + ProcessedEvents)
                                                                              └─ "book.renamed" ─► reviews.catalog-sync ─► reviews-api (bookTitle у MongoDB + processedEvents)
                                       відхилені/необроблені ─► catalog-events.dlx (direct) ─► <черга>.dlq
```

## Що де
| Вимога | Де в коді |
|---|---|
| RabbitMQ в AppHost (volume, Management UI) | `Platform.AppHost/AppHost.cs` — `AddRabbitMQ("messaging", password: …).WithDataVolume().WithManagementPlugin(port: 15672)`, `WithReference(rabbit).WaitFor(rabbit)` для catalog/loans/reviews |
| Aspire.RabbitMQ.Client (7.x), `IConnection`, health check | `Platform.Messaging/MessagingExtensions.cs` — `builder.AddRabbitMQClient("messaging")` (singleton `IConnection`, health check Aspire) + власна перевірка `rabbitmq-connection` (тег `ready`); асинхронний API `IChannel` |
| Декларативна топологія (topic exchange, черги, bindings, DLX/DLQ) | `Platform.Messaging/Topology/MessagingTopology.cs`; оголошується під час старту (`TopologyInitializer` і `RabbitMqEventConsumer`) |
| Незмінні integration events (EventId, CorrelationId) | `Platform.Messaging/Events/IntegrationEvent.cs`, `CatalogEvents.cs` — `record` з `init` |
| `IEventPublisher` + persistent-публікація в JSON | `Platform.Messaging/Publishing/` — `Persistent`, `ContentType`, `MessageId = EventId`, `Type`, `x-correlation-id`, `x-schema-version`; camelCase (`JsonDefaults`) |
| Публікація лише після збереження змін | `Library.Catalog.Bll/Services/BookService.cs` — `ChangePriceCoreAsync`, `RenameCoreAsync`: `SaveChangesAsync` → `PublishAsync` |
| Consumer як `BackgroundService`, ручне підтвердження | `Platform.Messaging/Consuming/RabbitMqEventConsumer.cs` — `BasicQos`, `AsyncEventingBasicConsumer`, `autoAck: false`, зупинка через `stoppingToken` |
| Ack / Nack без requeue | той самий файл: успіх → `BasicAckAsync`; JSON/несумісна версія → `BasicNackAsync(requeue: false)` → DLQ |
| Ідемпотентні обробники, `ProcessedEvents` | Видачі: `Library.Loans.Bll/Events/*Handler.cs`, `Library.Loans.Dal/Repositories/ProcessedEventRepository.cs`, таблиця в `Platform.AppHost/db/loans-init.sql`; Відгуки: `Library.Reviews.Infrastructure/Messaging/BookRenamedHandler.cs`, колекція `processedEvents` |
| Репліка даних за політикою Лаб. 01 | Видачі: `BookRepository.UpdatePriceAsync/UpdateTitleAsync`; Відгуки: `UpdateMany` по `bookId`; час останньої застосованої зміни (`PriceChangedAt`, `TitleChangedAt`, `bookTitleSyncedAt`) — захист від out-of-order |
| Повтори з `x-retry-count`, максимум спроб | `RabbitMqEventConsumer.RetryOrDeadLetterAsync` — повторна публікація зі збільшеним лічильником, оригінал підтверджується; після 3 спроб — DLQ |
| Передача контексту трасування | `Platform.Messaging/Telemetry/TraceContextPropagation.cs` — W3C `traceparent` (Inject/Extract), Activity `Producer` і `Consumer` з тегами `messaging.*`; `AddSource("Platform.Messaging")` у ServiceDefaults |
| Структуроване логування збоїв | Serilog `LogContext`: `EventId`, `EventType`, `CorrelationId`, `Queue`; ідентифікатори сутностей — у повідомленнях обробників; метрика `messaging.dlq.depth` (gauge) |

## Запуск
Як у Lab07, плюс **пароль RabbitMQ** (секрет, у репозиторій не потрапляє):
```powershell
cd Platform.AppHost
dotnet user-secrets set "Parameters:rabbit-password" "<пароль>"
cd ..
dotnet run --project Platform.AppHost -p:UseAppHost=false
```
У Dashboard має з'явитися ресурс **messaging** (Running). Management UI: **http://localhost:15672**, користувач `guest`, пароль — значення `rabbit-password`. Вкладка *Exchanges*: `catalog-events`, `catalog-events.dlx`; *Queues*: `loans.catalog-sync`, `reviews.catalog-sync` та відповідні `*.dlq`; у *Bindings* exchange `catalog-events` — `book.*` і `book.renamed`.

> Якщо Management UI показує порожній список черг після першого запуску — зачекайте кілька секунд: топологію оголошують сервіси при старті.

## Перевірка (контрольні точки)
Запити — через Gateway (`http://localhost:5000`); у PowerShell:
```powershell
# 1. Зміна ціни: за кілька секунд нова ціна з'являється в Books бази LibraryLoans, ціни в LoanItems не змінюються
Invoke-RestMethod -Method Put -Uri http://localhost:5000/api/books/1/price -ContentType application/json -Body '{"price":199.99}'

# 2. Перейменування: оновлюється Books.Title у видачах і bookTitle в усіх відгуках про книгу 1
Invoke-RestMethod -Method Put -Uri http://localhost:5000/api/books/1/title -ContentType application/json -Body '{"title":"Кобзар (нове видання)"}'
```
Перевірка даних:
* Видачі (SQL): `SELECT BookId, Title, Price, PriceChangedAt, TitleChangedAt FROM dbo.Books;` та `SELECT * FROM dbo.ProcessedEvents;`
* Відгуки (MongoDB, `GET /api/reviews?...` або Dashboard → mongo): `bookTitle` оновлено в усіх документах із `bookId = 1`.
* Management UI → *Queues*: після публікації повідомлення з'являється й швидко зникає з черг (Ack); `MessageId` = `EventId`, заголовок `x-correlation-id`.

### Сценарії стійкості
1. **Дублікат події.** Management UI → Exchanges → `catalog-events` → *Publish message*: Routing key `book.renamed`; Properties: `message_id` = `11111111-1111-1111-1111-111111111111`, `type` = `BookRenamedEvent`, `delivery_mode` = `2`; Payload:
   ```json
   {"bookId":1,"oldTitle":"A","newTitle":"Дублікат-тест","eventId":"11111111-1111-1111-1111-111111111111","occurredAt":"2026-11-24T12:00:00Z","correlationId":"manual-1","schemaVersion":1}
   ```
   Надішліть **двічі**: перша обробка змінює дані, друга — у логах `Duplicate event … skipped`, `ProcessedEvents` містить один запис. Значення `occurredAt` підставте **поточним UTC-часом**: подія зі старішою датою за вже застосовану пропускається як застаріла (`Skipped`), а дата з майбутнього заблокувала б наступні реальні зміни.
2. **Некоректний JSON → DLQ.** Те саме, але Payload `not a json`, `type` = `BookRenamedEvent`: повідомлення без повторів потрапляє в `loans.catalog-sync.dlq` та `reviews.catalog-sync.dlq` (заголовок `x-death` з причиною `rejected`); у логах `Permanent failure … sending to DLQ`; метрика `messaging.dlq.depth` > 0.
3. **Обробка після перезапуску `loans-api`.** Зупиніть `loans-api` у Dashboard, змініть кілька цін (запити вище) — повідомлення накопичуються в `loans.catalog-sync` (Management UI → *Ready*). Запустіть `loans-api` — усі події обробляються, лічильник у черзі повертається до 0.
4. **Тимчасовий збій → повтори.** Зупиніть контейнер `sql`, змініть ціну: у логах `loans-api` — `failed, retry 1/3`, `2/3`, `3/3` (заголовок `x-retry-count`), далі `sending to DLQ`; якщо SQL повернути до вичерпання спроб — подія оброблюється.
5. **Наскрізний trace.** Dashboard → Traces → запит `PUT /api/books/1/price`: spans `PUT books/{id}/price` → `catalog-events publish` (Producer) → `loans.catalog-sync process` (Consumer) → SQL-команди; у Structured logs фільтр за `CorrelationId`.

## Ключові рішення
* **Спільна бібліотека `Platform.Messaging`**: події, publisher, consumer, топологія і tracing — в одному місці; сервіси підключають її одним викликом `AddMessaging()` + `AddEventConsumer(queue, …)`.
* **Вся топологія оголошується кожним учасником** (ідемпотентно). Інакше подія, опублікована до першого запуску споживача, була б втрачена: exchange без bindings відкидає повідомлення.
* **DLX типу `direct`** з `x-dead-letter-routing-key` = ім'я черги (замість `fanout` із прикладу): кожна DLQ отримує лише повідомлення своєї черги.
* **Повтор — у ту саму чергу** (default exchange, routing key = ім'я черги), а не в `catalog-events`: інакше копію повтору отримали б усі споживачі.
* **Тимчасовий vs постійний збій.** Тимчасові (БД недоступна, таймаут) — повтори до 3 разів із паузою 1 с × номер спроби; постійні (`JsonException`, невідома версія схеми, відсутні обов'язкові поля) — одразу в DLQ. Подія, на яку сервіс не підписаний, підтверджується й ігнорується (логується попередження).
* **Out-of-order.** Кожна зміна копії застосовується лише якщо `OccurredAt` події новіший за останній застосований (окремо для ціни й назви).
* **Ідемпотентність**: SQL — `EventId` у `ProcessedEvents` (PRIMARY KEY) в одній транзакції зі зміною; MongoDB (автономний сервер без транзакцій) — перевірка `processedEvents` → ідемпотентна зміна → запис `_id = EventId`; збій між кроками дає лише безпечне повторне застосування.
* **Збій брокера після збереження в БД.** Збережена зміна не скасовується, а втрата події фіксується в логах (`Error … was NOT published`). Повну гарантію доставки дає *Transactional Outbox* (подія записується в таблицю тієї ж БД у тій самій транзакції, окремий фоновий процес публікує її) — не реалізовано (за завданням — за бажанням).
* **Версіонування схеми**: `schemaVersion` у тілі й заголовку `x-schema-version`; нові поля додаються, наявні не перейменовуються; споживач відхиляє версії новіші за підтримувану.
* **Обмеження**: події створення/видалення книги не публікуються (не потрібні для сценаріїв завдання; Видачі зберігають історію й `FK` без каскаду).

## Питання до захисту
* **gRPC чи події?** gRPC — синхронна відповідь потрібна тут і зараз (агрегація). Події — повідомити інших про факт без очікування відповіді, послабити зв'язність, пережити тимчасову недоступність споживача.
* **Eventual consistency**: копія в іншому сервісі оновлюється не миттєво, а за секунди; джерело істини — Каталог.
* **Чому події незмінні й самодостатні?** Це факти минулого; споживач не має ходити в Каталог по додаткові дані.
* **Exchange / queue / binding**: publisher надсилає в exchange з routing key; binding з шаблоном (`*` — одне слово, `#` — будь-яка кількість) спрямовує повідомлення в черги; кожна черга — окремий споживач (fan-out), екземпляри одного сервісу читають одну чергу (competing consumers).
* **Ідемпотентність** обов'язкова, бо брокер гарантує *at-least-once*: повідомлення може прийти повторно (обрив зʼєднання до Ack).
* **Чому повтор — це повторна публікація, а не requeue?** Повідомлення, повернене через requeue, приходить без змін — лічильник у заголовку збільшити не можна.
* **Dead Letter Exchange**: куди RabbitMQ переносить відхилені (Nack requeue:false) повідомлення; зберігає оригінал і заголовок `x-death`; потребує ручного розслідування.
* **Як працює trace через брокер?** `traceparent` у заголовках повідомлення: publisher — Inject, consumer — Extract і новий Activity `Consumer`, дочірній до span публікації.

---

# Lab07 (попередня робота) — Резильєнтність (Polly), бізнес-метрики та Health Checks

Розширення платформи з Lab06 (Aspire, YARP Gateway, Aggregator на gRPC, Redis-кеш):

* **resilience** для всіх вихідних викликів Aggregator: стандартний обробник (Polly) на кожен клієнт, окремі політики для критичних (Catalog, Loans) і некритичного (Reviews) сервісів, повтори gRPC за статусами;
* **бізнес-метрики** (`IMeterFactory`): лічильники, гістограми тривалості, `ObservableGauge` — у кожному сервісі;
* **health checks**: SQL Server, MongoDB, Redis (`Degraded`), gRPC Health Checking Protocol, перевірка залежностей Aggregator; окремі `/health/live` і `/health/ready`;
* **AppHost**: `WithHttpHealthCheck("/health/ready")` + `WaitFor` — залежні ресурси стартують лише після `Healthy`.

## Що де
| Вимога | Де в коді |
|---|---|
| Standard Resilience Handler для всіх клієнтів | `Platform.ServiceDefaults/Extensions.cs` — `ConfigureHttpClientDefaults(… AddStandardResilienceHandler)` (базовий, для всіх `HttpClient`); у Aggregator клієнти перевизначаються через `RemoveAllResilienceHandlers()` + власний `AddStandardResilienceHandler` — обробники не нашаровуються (`Platform.Aggregator/Resilience/ResilienceExtensions.cs`, `UseResiliencePolicy`) |
| Повтори gRPC за статусами | `ResilienceExtensions.UseGrpcRetries` — `ServiceConfig` з `RetryPolicy` (`Unavailable`), `MethodName.Default` |
| Retry / Circuit Breaker / Timeouts | `UseResiliencePolicy`: експоненційна затримка + jitter, `DisableForUnsafeHttpMethods`, `FailureRatio`, `MinimumThroughput`, `SamplingDuration`, `BreakDuration`, `OnRetry`/`OnOpened`/`OnHalfOpened`/`OnClosed`/`OnTimeout` з логуванням (категорія `Polly`) |
| Різні політики критичних/некритичних | `Platform.Aggregator/Resilience/ResiliencePolicy.cs` (`ResiliencePolicies.Loans/Catalog/Reviews`), підключення — `Platform.Aggregator/Program.cs` |
| Graceful degradation Redis | `Library.Catalog.Bll/Caching/TwoLevelCache.cs` (L1 → L2 → БД, помилка Redis логується й не ламає запит), `LoanAggregationService` (кеш агрегатів необов'язковий) |
| Бізнес-метрики | `Library.Loans.Bll/Telemetry/LoanMetrics.cs`, `Library.Catalog.Bll/Telemetry/CatalogMetrics.cs`, `Library.Reviews.Application/Common/Telemetry/ReviewMetrics.cs` + `MetricsBehavior`, `Platform.Aggregator/Telemetry/AggregatorMetrics.cs` |
| Реєстрація Meter у OpenTelemetry | `Platform.ServiceDefaults/Extensions.cs` — `AddMeter("Loans.Api","Catalog.Api","Reviews.Api","Aggregator.Api")`, `AddMeter("Library.*","Platform.*")`, `AddMeter("Polly")` |
| Health checks БД | `AddSqlServer` (`loans-db`, `catalog-db`), `AddMongoDb` (`reviews-db`) — теги `database`/`ready`, `timeout` 5 с |
| Redis health check | `catalog-api` і `aggregator`: `AddRedis(... failureStatus: Degraded)` (`redis-cache`); вбудовану перевірку Aspire вимкнено (`DisableHealthChecks = true`) |
| gRPC Health Checking Protocol | сервіси: `Platform.ServiceDefaults/GrpcSupport/GrpcHealthExtensions.cs` (`AddGrpcHealthChecks`, `MapGrpcHealthChecksService`); Aggregator: `HealthChecks/GrpcDependencyHealthCheck.cs` (`Health.HealthClient.Check`, дедлайн 2 с) |
| `/health/live`, `/health/ready` | `Platform.ServiceDefaults/Extensions.cs` — `MapDefaultEndpoints`: доступні в усіх середовищах, JSON-відповідь з деталями кожної перевірки |
| AppHost | `Platform.AppHost/AppHost.cs` — `WithHttpHealthCheck("/health/ready")`, `WaitFor(...)` |

## Запуск
Як у Lab06 (`dotnet dev-certs https --trust`, `dotnet user-secrets set "Parameters:sql-password" "<пароль>"` у `Platform.AppHost`, `dotnet run --project Platform.AppHost -p:UseAppHost=false`). У Dashboard → **Resources** усі ресурси мають стати *Healthy* (стовпець *State* / health-значок).

## Політики resilience (Aggregator)
Для кожного клієнта політика однакова на двох рівнях: HTTP (мережеві збої, таймаути, 5xx/408/429 — Standard Resilience Handler) і gRPC (повтори за статусом у `ServiceConfig`, бо gRPC-статус іде в трейлерах відповіді HTTP 200 і HTTP-обробник його не бачить).

| Параметр | loans-api (критичний) | catalog-api (критичний) | reviews-api (некритичний) | Обґрунтування |
|---|---|---|---|---|
| Retry: кількість | 3 | 4 | 1 | Читання ідемпотентні; для критичних — більше спроб, для некритичного швидше здатися й віддати часткову відповідь |
| Retry: затримка / тип | 200 мс, експоненційна, jitter | 200 мс, експоненційна, jitter | 100 мс, експоненційна, jitter | Експонента знижує навантаження на збійний сервіс; jitter розводить повтори різних клієнтів у часі |
| Небезпечні методи | `DisableForUnsafeHttpMethods` | те саме | те саме | POST/PUT/DELETE не повторюються — неідемпотентна дія не виконається двічі |
| Timeout однієї спроби | 3 с | 3 с | 1,5 с | Некритичний сервіс не має права затримувати відповідь |
| Загальний timeout | 12 с | 15 с | 4 с | ≈ спроби × AttemptTimeout + паузи; узгоджено з retry |
| Circuit breaker: FailureRatio | 0,3 | 0,3 | 0,7 | Критичний — швидко «розімкнути» й не добивати збійний сервіс; для некритичного — терпимість до окремих збоїв |
| MinimumThroughput | 10 | 10 | 5 | Мінімум запитів у вікні, щоб рішення було статистично значущим |
| SamplingDuration | 30 с | 30 с | 15 с | Не менше 2 × AttemptTimeout (вимога Polly) |
| BreakDuration | 10 с | 10 с | 15 с | Критичним швидше даємо шанс відновитися; некритичному — довша пауза |
| gRPC retry (MaxAttempts / backoff) | 3 / 200 мс → 2 с ×2 | 4 / 200 мс → 2 с ×2 | 2 / 100 → 500 мс ×2 | Повтор лише для `Unavailable` (транзієнтна недоступність) |
| gRPC deadline виклику | 8 с | 10 с | 3 с | Усі спроби разом мають вкластися в загальний timeout |

Колбеки Polly логуються (категорія `Polly`): `OnRetry` — Warning, `OnOpened` — Error, `OnHalfOpened` — Warning, `OnClosed` — Information, `OnTimeout` — Error. Polly також публікує метрику `resilience.polly.strategy.events` (`event.name` = `OnRetry`, `OnCircuitOpened`, …) — Meter `Polly` додано в ServiceDefaults.

**Graceful degradation Redis.** Збій Redis не повинен ламати запит: `TwoLevelCache` ловить помилки L2, логує попередження й іде далі до БД (L1/БД залишаються працездатними); Aggregator так само працює без кешу агрегатів. Health check Redis повертає `Degraded` (не `Unhealthy`) — сервіс залишається в ротації.

## Бізнес-метрики
Кожен клас метрик — **Singleton**, `Meter` створюється через `IMeterFactory` (ім'я Meter — на сервіс). Теги мають низьку кардинальність (операція, результат, транспорт); id сутностей у тегах не використовуються.

| Сервіс (Meter) | Інструмент | Тип | Теги |
|---|---|---|---|
| `Loans.Api` | `loans.operations` | Counter | `loans.operation` (create/return/cancel), `loans.result` |
| | `loans.books.issued` | Counter | — |
| | `loans.operation.duration` (мс) | Histogram | як `loans.operations` |
| | `loans.operations.in_flight` | ObservableGauge | — |
| `Catalog.Api` | `catalog.operations`, `catalog.operation.duration`, `catalog.copies.written_off`, `catalog.operations.in_flight` | Counter / Histogram / Counter / Gauge | `catalog.operation`, `catalog.result` |
| `Reviews.Api` | `reviews.operations`, `reviews.operation.duration`, `reviews.operations.in_flight` | Counter / Histogram / Gauge | `reviews.operation`, `reviews.kind`, `reviews.result` |
| `Aggregator.Api` | `aggregator.requests`, `aggregator.dependency.failures`, `aggregator.operation.duration`, `aggregator.requests.in_flight` | Counter / Counter / Histogram / Gauge | `aggregator.transport` (grpc/http), `aggregator.result` (ok/partial/not_found/unavailable/error), `aggregator.dependency` |

Гістограми записують тривалість і успішних, і невдалих операцій (тег `*.result` розрізняє). Перегляд: Dashboard → **Metrics** → ресурс → потрібний Meter.

## Health checks
| Endpoint | Що перевіряє | Коли 200 / 503 |
|---|---|---|
| `/health/live` | лише «процес живий» (перевірка `self`, без залежностей) | 200, поки процес відповідає |
| `/health/ready` | залежності з тегом `ready`: БД, Redis, downstream-сервіси | `Healthy` і `Degraded` → **200**; `Unhealthy` → **503** |
| `/health`, `/alive` | усі перевірки / лише live — додатково, **тільки в Development** | |

Відповідь — JSON: `status`, `totalDurationMs`, `checks[]` (`name`, `status`, `durationMs`, `description`, `tags`, `data`; текст помилки — лише в Development).

Перевірки за сервісами:
* **loans-api** — `loans-db` (SQL, Unhealthy, 5 с);
* **catalog-api** — `catalog-db` (SQL, Unhealthy), `redis-cache` (Redis, **Degraded**), `cache` (статистика, без тегу `ready`);
* **reviews-api** — `reviews-db` (MongoDB, Unhealthy);
* **aggregator** — `loans-api`, `catalog-api` (gRPC Health, **Unhealthy**), `reviews-api` (gRPC Health, **Degraded**), `redis-cache` (**Degraded**).

Усі сервіси також відповідають на `grpc.health.v1.Health/Check` (`Serving`/`NotServing` — за тими ж перевірками з тегом `ready`, звіт оновлюється раз на 10 с). Aggregator викликає `Health.HealthClient.CheckAsync` із дедлайном 2 с.

## Сценарії перевірки
1. **Усе працює.** `https://localhost:<порт aggregator>/health/ready` → `Healthy`; у Dashboard усі ресурси *Healthy*.
2. **Зупинка Redis** (Dashboard → redis → Stop): `/health/ready` Aggregator і Catalog → `Degraded`, HTTP **200**; запити `/api/aggregator/loans/1/full` продовжують працювати (без кешу).
3. **Зупинка reviews-api**: Aggregator `/health/ready` → `Degraded` (reviews), відповідь `/full` — 200 з `warnings`; у логах `Polly` — `OnRetry`, після серії збоїв `OnCircuitOpened` — наступні виклики одразу відхиляються без очікування.
4. **Сповільнення reviews**: перед запуском AppHost `$env:CHAOS_REVIEWS_DELAY_MS="5000"` → кожен виклик перевищує timeout (дедлайн 3 с) → повтори, `warnings`, відкриття breaker.
5. **Зупинка loans-api**: Aggregator `/health/ready` → **503** (`Unhealthy`), `/full` → 503.
6. **Метрики Polly**: Dashboard → Metrics → aggregator → Meter `Polly` → `resilience.polly.strategy.events` (фільтр `event.name`: `OnRetry` / `OnCircuitOpened`).
7. **Бізнес-метрики**: створіть видачу/зробіть запити до `/api/aggregator/...` → Metrics → `loans.operations`, `aggregator.requests`, `aggregator.operation.duration`.

## Питання до захисту
* **Чому HTTP-обробник не повторює gRPC-помилки?** gRPC-статус передається в трейлерах відповіді HTTP 200; повтори за статусами налаштовуються в `ServiceConfig` каналу.
* **Чому `RemoveAllResilienceHandlers`?** Базовий обробник з `ConfigureHttpClientDefaults` додається всім клієнтам; без видалення політики нашаруються (подвійні повтори, два breaker).
* **Чому `SamplingDuration` ≥ 2 × `AttemptTimeout`?** Вимога валідації Polly: вікно має вміщати кілька спроб.
* **Чому Redis — `Degraded`, а SQL — `Unhealthy`?** Без кешу сервіс повільніший, але працює; без БД — ні.
* **Чому liveness не перевіряє залежності?** Інакше збій БД спричинить перезапуск здорового процесу, не виправивши причину.
* **Чому `DisableForUnsafeHttpMethods`?** Повтор POST може виконати дію двічі.

---

# Lab06 (попередня робота) — gRPC-виклики та двохрівневе кешування (Memory + Redis)

Розширення платформи з Lab05 (.NET Aspire, YARP Gateway, Aggregator, OpenTelemetry, Serilog):

* **двохрівневий кеш** у сервісі Каталогу: L1 — `IMemoryCache`, L2 — Redis (`IDistributedCache` через Aspire);
* **gRPC-контракти** (`.proto`) і gRPC-сервіси в `loans-api`, `catalog-api`, `reviews-api`;
* **Aggregator** викликає сервіси через gRPC паралельно (`Task.WhenAll`), а готову відповідь кешує в Redis на 30 с;
* **спостережуваність**: gRPC-spans у trace, серверний interceptor з логуванням, метрики `cache.hits` / `cache.misses`.

Зовнішні клієнти, як і раніше, працюють через Gateway по HTTP: **http://localhost:5000**.

```
Клієнт ──HTTP──► gateway :5000 ──► /api/books|authors|categories → catalog-api ─► L1 → L2 (Redis) → SQL
                            ├──► /api/loans|readers   → loans-api   → SQL
                            ├──► /api/reviews         → reviews-api → MongoDB
                            └──► /api/aggregator/**   → aggregator ──gRPC (HTTP/2)──► loans-api
                                                          │                          ├► catalog-api   (паралельно)
                                                          └─ кеш у Redis (30 с)      └► reviews-api   (паралельно)
```

## Що де
| Частина завдання | Де в коді |
|---|---|
| 1.2 Redis в AppHost | `Platform.AppHost/AppHost.cs` — `AddRedis("redis").WithDataVolume()`, `WithReference(redis).WaitFor(redis)` для `catalog-api` і `aggregator` |
| 1.3 `.proto` | `Services/*/…/Protos/{catalog,loans,reviews}.proto`; у сервісах `GrpcServices="Server"`, в Aggregator — `Client` |
| 2 `IMemoryCache` | `Library.Catalog.Api/Program.cs` (`AddMemoryCache`, `SizeLimit`, `CompactionPercentage`), `Library.Catalog.Bll/Caching/` |
| 2.2–2.4 cache-aside, інвалідація, логи | `BookService.GetByIdAsync` / `GetByCategoryAsync`; `InvalidateBookAsync` після `ChangePrice` / `WriteOff` / `Delete` / `Create` |
| 3 Redis, L1→L2→БД | `AddRedisDistributedCache("redis")`, `Caching/TwoLevelCache.cs`, TTL — `Caching/CachePolicy.cs` |
| 4 gRPC-сервіси | `GrpcServices/CatalogGrpcService.cs`, `LoansGrpcService.cs`, `ReviewsGrpcService.cs` |
| 5 Aggregator на gRPC | `Platform.Aggregator/Program.cs` (`AddGrpcClient`), `Services/LoanAggregationService.cs` |
| 6.1 трасування | `AddAspNetCoreInstrumentation` / `AddHttpClientInstrumentation` у ServiceDefaults — gRPC-spans з `rpc.service`, `rpc.method` |
| 6.2 логування | `Platform.ServiceDefaults/GrpcSupport/GrpcLoggingInterceptor.cs` |
| 6.3 метрики | `Caching/CacheMetrics.cs` (Каталог), `Caching/AggregatorCacheMetrics.cs`; `AddMeter("Library.*", "Platform.*")` у ServiceDefaults |

## Запуск
Потрібно: .NET 10 SDK, **Docker Desktop**.

1. Один раз — dev-сертифікат (gRPC працює поверх HTTPS/HTTP/2, сервіси слухають і HTTP, і HTTPS):
   ```powershell
   dotnet dev-certs https --trust
   ```
2. Пароль SQL Server (секрет, у репозиторій не потрапляє):
   ```powershell
   cd Platform.AppHost
   dotnet user-secrets set "Parameters:sql-password" "<ваш_пароль>"
   ```
3. Міграції Catalog мають лежати в `Services/Catalog/Library.Catalog.Dal/Migrations` (сервіс застосовує їх при старті).
4. Запуск:
   ```powershell
   dotnet run --project Platform.AppHost -p:UseAppHost=false
   ```
   У Dashboard усі ресурси, зокрема **redis**, мають бути в статусі *Running*.

## Кешування
**Ключі:** `catalog:product:{id}` (картка книги), `catalog:category:{id}:products` (книги категорії), `aggregator:loan:{id}:full` (агрегат). Префікс ізолює ключі сервісів у спільному Redis.

| Дані | L1 (пам'ять) | L2 (Redis) | Чому |
|---|---|---|---|
| Картка книги | sliding 1 хв, absolute 3 хв | absolute 10 хв | читається часто → sliding; змінюється рідко |
| Книги категорії | absolute 2 хв, пріоритет High | absolute 15 хв | довідникові дані |
| Агрегат видачі (Aggregator) | — | absolute **30 с** | агрегат не має виглядати «свіжішим» за джерела |

* **L1 завжди коротший за L2** — `CachePolicy` не дозволяє створити політику з L1 ≥ L2 (інакше застарілі дані в пам'яті переживуть оновлення в Redis).
* `SizeLimit = 10 240` (одиниці `Size` = KiB серіалізованого значення); при переповненні витісняється 25 % записів, першими — записи з нижчим пріоритетом.
* **Інвалідація синхронно із записом:** після `PUT /api/books/{id}/price`, `POST …/write-off`, `DELETE`, `POST` видаляються картка книги та список її категорії з **обох** рівнів; у логу — ключ і причина (`reason`).
* L1 — власний для кожного екземпляра: інші репліки побачать зміну після спливу L1 TTL (до 3 хв) або одразу через L2. Pub/sub для оновлення L1 не використовується (необов'язкова частина).
* Не кешуються: «не знайдено», часткові агреговані відповіді, персональні дані.
* **Redis недоступний** → сервіс не падає: читає з БД (повільніше), помилка логується, кілька секунд Redis не опитується; інвалідація, яку не вдалося виконати, повторюється після відновлення.
* Статистика кешу (hit ratio L1/L2, кількість інвалідацій) — у `/health` Каталогу (перевірка `cache`).

## Перевірка (контрольні точки)
```powershell
# 1. Двічі той самий товар — у trace другого запиту немає SQL-span
curl http://localhost:5000/api/books/1
curl http://localhost:5000/api/books/1

# 2. Зміна ціни → наступний GET повертає нові дані
curl -X PUT http://localhost:5000/api/books/1/price -H "Content-Type: application/json" -d '{"price":199.90}'
curl http://localhost:5000/api/books/1

# 3. Агрегат: gRPC + кеш Redis (повторити двічі) і порівняння з HTTP (Lab05)
curl -i http://localhost:5000/api/aggregator/loans/1/full      -H "X-Correlation-Id: demo-grpc"
curl -i http://localhost:5000/api/aggregator/loans/1/full-http -H "X-Correlation-Id: demo-http"
```
Попередньо створити видачу (читач 1, книги 1 і 3) і відгук — запити з Lab05 лишилися без змін (`POST /api/loans`, `POST /api/reviews`).

* **Trace повторного запиту:** Dashboard → Traces → `catalog-api` → `GET /api/books/1`; перший запит містить `cache.get` + SQL-span, другий — лише `cache.get` з тегом `cache.result = hit-l1`.
* **Trace агрегату:** `gateway` → `aggregator` (`GetFullLoan`) → gRPC-spans клієнта й сервера (`rpc.service`, `rpc.method`) в одному trace → `loans-api` / `catalog-api` / `reviews-api` → SQL / Mongo / Redis. Кеш-хіт агрегату — trace без gRPC-викликів.
* **Structured logs:** `Cache hit L1/L2`, `Cache miss`, `Cache invalidated {Key}, reason {Reason}`, `gRPC {Method} from {Peer}: {GrpcStatus} in {Elapsed} ms`. Однаковий `X-Correlation-Id` в усіх сервісах (передається в метаданих gRPC-виклику `CorrelationIdHandler`-ом).
* **Metrics:** ресурс `catalog-api` → `cache.hits`, `cache.misses` (тег `cache.level` = L1/L2), `cache.invalidations`, `cache.evictions`, `cache.l1.size`, `cache.operation.duration`; ресурс `aggregator` → `cache.hits`, `cache.misses`.
* **Redis зупинено** (Dashboard → redis → Stop): запити працюють, у логах `Redis (L2) … failed … falling back to the database`.
* **Часткова відмова:** зупинити `reviews-api` → `/full` повертає **200** з `"warnings": ["reviews-api is unavailable (Unavailable)"]` (такі відповіді не кешуються); `loans-api` недоступний → **503**; видачі немає → **404**.
* **Сумісність proto:** змінити номер поля (наприклад, `title = 3` → `33`) лише в `catalog.proto` сервера й перезапустити сервіс — Aggregator перестає коректно читати поле; повернути номер назад.

## Порівняння тривалості HTTP і gRPC
`/full-http` — агрегація Lab05 (HTTP REST, без кешу), `/full` — gRPC (перший виклик — без кешу, другий — із Redis). Тривалість береться зі span `GET /api/aggregator/…` сервісу `aggregator` у Traces (кілька прогонів, перший «холодний» пропускається).

| Сценарій | Тривалість, мс |
|---|---|
| HTTP REST (`/full-http`) | _заповнити за Traces_ |
| gRPC, без кешу агрегату (`/full`, перший виклик) | _заповнити_ |
| gRPC, кеш агрегату (`/full`, повторний виклик) | _заповнити_ |

Чому gRPC швидший: один HTTP/2-канал на сервіс мультиплексує паралельні виклики, бінарний protobuf замість JSON, один виклик `GetBooksByIds` / `GetReviewsByBookIds` замість N запитів.

## Контракти
* Поля `.proto` нумеруються назавжди; гроші — `Money { units, nanos }`, а не `double`; дати — `google.protobuf.Timestamp` (UTC).
* gRPC-сервіси — тонкі адаптери над тим самим BLL, що й REST-контролери: `EntityNotFoundException`/`NotFoundException` → `NotFound`, невалідні вхідні дані → `InvalidArgument`, конфлікт → `FailedPrecondition`, недоступність БД → `Unavailable`. Непередбачені винятки interceptor перетворює на `Internal` без деталей.
* Aggregator: `AddGrpcClient<…>` з адресами `https://catalog-api` / `loans-api` / `reviews-api` (service discovery), дедлайн 5 с і `CancellationToken` у кожному виклику; `RpcException` необов'язкових сервісів → попередження.
* У Development gRPC-клієнти Aggregator приймають самопідписаний dev-сертифікат (у Production — ні).

---

# Основа: Lab05 — Мікросервісна платформа на .NET Aspire

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
Кроки запуску — у розділі «Запуск» вище (Lab06): ті самі, плюс Redis та dev-сертифікат.

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
Зупиніть `reviews-api` у Dashboard і повторіть `.../aggregator/loans/1/full`: статус **200**, у відповіді дані видачі й каталогу та `"warnings": ["reviews-api is unavailable (Unavailable)"]`. Якщо недоступний `loans-api` — **503**, якщо видачі немає — **404**.

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

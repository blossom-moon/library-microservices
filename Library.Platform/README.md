# Lab07 — Резильєнтність (Polly), бізнес-метрики та Health Checks

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

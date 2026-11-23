var builder = DistributedApplication.CreateBuilder(args);

// ---------- Інфраструктура: контейнери з постійними томами ----------
// Пароль — секретний параметр: dotnet user-secrets set "Parameters:sql-password" "<пароль>"
var sqlPassword = builder.AddParameter("sql-password", secret: true);

var sql = builder.AddSqlServer("sql", sqlPassword)
    .WithDataVolume("library-sql-data")
    .WithLifetime(ContainerLifetime.Persistent);

// Імена баз = імена рядків підключення, які читають сервіси (ConnectionStrings__<name>)
var loansDb = sql.AddDatabase("LibraryLoans")
    .WithCreationScript(File.ReadAllText(Path.Combine(builder.AppHostDirectory, "db", "loans-init.sql")));
var catalogDb = sql.AddDatabase("CatalogDb");

var mongo = builder.AddMongoDB("mongo")
    .WithDataVolume("library-mongo-data")
    .WithLifetime(ContainerLifetime.Persistent);
var reviewsDb = mongo.AddDatabase("ReviewsDb");

// Лаб. 06: Redis — L2-кеш Каталогу та кеш агрегованих відповідей; том зберігає дані між перезапусками.
// Ім'я ресурсу "redis" = ім'я в AddRedisDistributedCache("redis"); рядок підключення — ConnectionStrings:redis.
var redis = builder.AddRedis("redis")
    .WithDataVolume("library-redis-data")
    .WithLifetime(ContainerLifetime.Persistent);

// ---------- Сервіси: БД → сервіси → Aggregator → Gateway ----------
// Лаб. 07: WithHttpHealthCheck("/health/ready") — Aspire опитує readiness-endpoint; статус ресурсу у Dashboard
// стає Healthy лише після успішних перевірок залежностей, і лише тоді WaitFor пропускає залежні ресурси.
// Демонстрація стійкості: CHAOS_REVIEWS_DELAY_MS=5000 (змінна середовища перед запуском AppHost) штучно
// сповільнює Reviews → timeout/retry/circuit breaker в Aggregator.
var chaosDelayMs = Environment.GetEnvironmentVariable("CHAOS_REVIEWS_DELAY_MS") ?? "0";

var loansApi = builder.AddProject<Projects.Library_Loans_Api>("loans-api")
    .WithReference(loansDb).WaitFor(loansDb)
    .WithHttpHealthCheck("/health/ready");

var catalogApi = builder.AddProject<Projects.Library_Catalog_Api>("catalog-api")
    .WithReference(catalogDb).WaitFor(catalogDb)
    .WithReference(redis).WaitFor(redis)
    .WithHttpHealthCheck("/health/ready");

var reviewsApi = builder.AddProject<Projects.Library_Reviews_WebApi>("reviews-api")
    .WithReference(reviewsDb).WaitFor(reviewsDb)
    .WithEnvironment("Chaos__DelayMs", chaosDelayMs)
    .WithHttpHealthCheck("/health/ready");

var aggregator = builder.AddProject<Projects.Platform_Aggregator>("aggregator")
    .WithReference(loansApi).WaitFor(loansApi)
    .WithReference(catalogApi).WaitFor(catalogApi)
    .WithReference(reviewsApi).WaitFor(reviewsApi)
    .WithReference(redis).WaitFor(redis)
    .WithHttpHealthCheck("/health/ready");

// Єдина публічна точка входу з фіксованим портом 5000
builder.AddProject<Projects.Platform_ApiGateway>("gateway")
    .WithReference(aggregator).WaitFor(aggregator)
    .WithReference(loansApi).WithReference(catalogApi).WithReference(reviewsApi)
    .WithHttpEndpoint(port: 5000, name: "public")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health/ready");

builder.Build().Run();

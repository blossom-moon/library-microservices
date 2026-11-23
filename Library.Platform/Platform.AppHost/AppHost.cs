var builder = DistributedApplication.CreateBuilder(args);

// Лаб. 10: режим публікації — коли маніфест генерується для Kubernetes (aspirate generate), а не для локального запуску.
// У ньому ресурси налаштовуються для кластера: внутрішні адреси замість localhost, cleartext gRPC на окремому порту,
// ініціалізація БД самим сервісом, без другого SQL Server і без Keycloak (див. README, «Відмінності publish-режиму»).
var publish = builder.ExecutionContext.IsPublishMode;

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
var identityDb = sql.AddDatabase("IdentityDb");   // Лаб. 09: користувачі, ролі та refresh-токени Auth Service

// Лаб. 09: окремий SQL Server для IdentityServer (користувачі + Configuration/Operational store) — ізольований від Auth Service
// У publish-режимі — той самий SQL Server (ще один контейнер з ≥ 2 ГБ пам'яті надто важкий для локального кластера).
var identityServerSql = publish
    ? sql
    : builder.AddSqlServer("identityserver-sql", sqlPassword)
        .WithDataVolume("library-identityserver-sql-data")
        .WithLifetime(ContainerLifetime.Persistent);
var identityServerDb = identityServerSql.AddDatabase("IdentityServerDb");

var mongo = builder.AddMongoDB("mongo")
    .WithDataVolume("library-mongo-data")
    .WithLifetime(ContainerLifetime.Persistent);
var reviewsDb = mongo.AddDatabase("ReviewsDb");

// Лаб. 06: Redis — L2-кеш Каталогу та кеш агрегованих відповідей; том зберігає дані між перезапусками.
// Ім'я ресурсу "redis" = ім'я в AddRedisDistributedCache("redis"); рядок підключення — ConnectionStrings:redis.
var redis = builder.AddRedis("redis")
    .WithDataVolume("library-redis-data")
    .WithLifetime(ContainerLifetime.Persistent);

// Лаб. 08: RabbitMQ — брокер повідомлень для подієвої інтеграції сервісів.
// Пароль — секретний параметр (user-secrets), бо з постійним томом згенерований щоразу новий пароль не збігся б зі збереженим.
// Management UI — http://localhost:15672 (користувач guest, пароль — значення параметра rabbit-password).
var rabbitPassword = builder.AddParameter("rabbit-password", secret: true);
var rabbit = builder.AddRabbitMQ("messaging", password: rabbitPassword)
    .WithDataVolume("library-rabbitmq-data")      // черги й повідомлення переживають перезапуск
    .WithManagementPlugin(port: 15672)            // веб-інтерфейс керування
    .WithLifetime(ContainerLifetime.Persistent);

// Лаб. 09: Keycloak (Authorization Services) — realm LibraryRealm створюється Pulumi-проєктом Pulumi/Library.Keycloak.Pulumi.
// Адмін-пароль — секретний параметр (user-secrets), бо з постійним томом згенерований пароль не збігся б зі збереженим.
// Лаб. 10: у Kubernetes Keycloak не розгортається (не входить до платформи завдання; його схема перевірки токенів у кластері вимкнена).
if (!publish)
{
    var keycloakAdminPassword = builder.AddParameter("keycloak-admin-password", secret: true);
    builder.AddKeycloak("keycloak", port: 8080, adminPassword: keycloakAdminPassword)
        .WithDataVolume("library-keycloak-data")
        .WithLifetime(ContainerLifetime.Persistent);
}

// Лаб. 09: параметри безпеки. Секрет підпису локальних JWT (HS256, ≥ 32 символи): dotnet user-secrets set "Parameters:jwt-secret" "<...>"
var jwtSecret = builder.AddParameter("jwt-secret", secret: true);
var identityServerClientSecret = builder.AddParameter("aggregator-client-secret", secret: true);
const string identityServerAuthority = "http://localhost:5400";
const string keycloakAuthority = "http://localhost:8080/realms/LibraryRealm";

// Спільні налаштування перевірки JWT для всіх ресурсів, що приймають токени (Local / IdentityServer / Keycloak)
IResourceBuilder<T> WithAuth<T>(IResourceBuilder<T> resource) where T : IResourceWithEnvironment
{
    resource
        .WithEnvironment("Jwt__Issuer", "library-auth")
        .WithEnvironment("Jwt__Audience", "library-api")
        .WithEnvironment("Jwt__SecretKey", jwtSecret);

    if (publish)
    {
        // Kubernetes: метадані IdentityServer — за DNS-іменем Service; iss токена лишається IssuerUri сервера
        resource
            .WithEnvironment("IdentityServer__Authority", "http://identityserver:8080")
            .WithEnvironment("IdentityServer__Issuer", identityServerAuthority)
            .WithEnvironment("Security__RequireHttpsMetadata", "false");
    }
    else
    {
        resource
            .WithEnvironment("IdentityServer__Authority", identityServerAuthority)
            .WithEnvironment("Keycloak__Authority", keycloakAuthority);
    }
    return resource;
}

// Лаб. 10: gRPC у Kubernetes — окремий cleartext HTTP/2 порт 8081 (іменований endpoint "grpc", див. UseCleartextGrpcPort)
IResourceBuilder<ProjectResource> WithGrpcPort(IResourceBuilder<ProjectResource> project) =>
    publish
        ? project.WithEndpoint(name: "grpc", scheme: "http", targetPort: 8081)
                 .WithEnvironment("Grpc__Cleartext", "true")
        : project;

// ---------- Сервіси: БД → сервіси → Aggregator → Gateway ----------
// Лаб. 07: WithHttpHealthCheck("/health/ready") — Aspire опитує readiness-endpoint; статус ресурсу у Dashboard
// стає Healthy лише після успішних перевірок залежностей, і лише тоді WaitFor пропускає залежні ресурси.
// Демонстрація стійкості: CHAOS_REVIEWS_DELAY_MS=5000 (змінна середовища перед запуском AppHost) штучно
// сповільнює Reviews → timeout/retry/circuit breaker в Aggregator.
var chaosDelayMs = Environment.GetEnvironmentVariable("CHAOS_REVIEWS_DELAY_MS") ?? "0";

// Auth Service (Identity + JWT) і IdentityServer (OAuth 2.0 / OIDC) — Лаб. 09
var authApi = WithAuth(builder.AddProject<Projects.Library_Auth_Api>("auth-api")
    .WithReference(identityDb).WaitFor(identityDb)
    .WithHttpHealthCheck("/health/ready"));

_ = builder.AddProject<Projects.Library_IdentityServer>("identityserver")
    .WithReference(identityServerDb).WaitFor(identityServerDb)
    .WithEnvironment("Clients__AggregatorSecret", identityServerClientSecret)
    .WithHttpHealthCheck("/health/ready");

var loansApi = WithAuth(WithGrpcPort(builder.AddProject<Projects.Library_Loans_Api>("loans-api"))
    .WithReference(loansDb).WaitFor(loansDb)
    .WithReference(rabbit).WaitFor(rabbit)         // споживач подій Каталогу (loans.catalog-sync)
    .WithEnvironment("Database__InitializeOnStartup", publish ? "true" : "false")   // Лаб. 10: у Kubernetes БД створює сам сервіс
    .WithHttpHealthCheck("/health/ready"));

var catalogApi = WithAuth(WithGrpcPort(builder.AddProject<Projects.Library_Catalog_Api>("catalog-api"))
    .WithReference(catalogDb).WaitFor(catalogDb)
    .WithReference(rabbit).WaitFor(rabbit)         // публікує події (exchange catalog-events)
    .WithReference(redis).WaitFor(redis)
    .WithHttpHealthCheck("/health/ready"));

var reviewsApi = WithAuth(WithGrpcPort(builder.AddProject<Projects.Library_Reviews_WebApi>("reviews-api"))
    .WithReference(reviewsDb).WaitFor(reviewsDb)
    .WithReference(rabbit).WaitFor(rabbit)         // споживач подій Каталогу (reviews.catalog-sync)
    .WithEnvironment("Chaos__DelayMs", chaosDelayMs)
    .WithHttpHealthCheck("/health/ready"));

// Лаб. 09: заглушка платіжного сервісу (ресурс payments-api у Keycloak)
var paymentsApi = WithAuth(builder.AddProject<Projects.Library_Payments_Api>("payments-api")
    .WithHttpHealthCheck("/health/ready"));

var aggregator = WithAuth(builder.AddProject<Projects.Platform_Aggregator>("aggregator")
    .WithEnvironment("Grpc__Cleartext", publish ? "true" : "false")   // Лаб. 10: адреси gRPC-клієнтів (https локально, http://_grpc.<сервіс> у Kubernetes)
    .WithReference(loansApi).WaitFor(loansApi)
    .WithReference(catalogApi).WaitFor(catalogApi)
    .WithReference(reviewsApi).WaitFor(reviewsApi)
    .WithReference(redis).WaitFor(redis)
    // Client Credentials: Aggregator отримує токен в IdentityServer (без користувача) для викликів downstream-сервісів
    .WithEnvironment("ClientCredentials__Authority", publish ? "http://identityserver:8080" : identityServerAuthority)
    .WithEnvironment("ClientCredentials__ClientId", "aggregator_service")
    .WithEnvironment("ClientCredentials__ClientSecret", identityServerClientSecret)
    .WithEnvironment("ClientCredentials__Scope", "loans.read catalog.read reviews.read")
    .WithHttpHealthCheck("/health/ready"));

// Єдина публічна точка входу з фіксованим портом 5000
var gateway = WithAuth(builder.AddProject<Projects.Platform_ApiGateway>("gateway")
    .WithReference(aggregator).WaitFor(aggregator)
    .WithReference(loansApi).WithReference(catalogApi).WithReference(reviewsApi)
    .WithReference(authApi).WithReference(paymentsApi)
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health/ready"));

// Фіксований порт 5000 — лише для локального запуску; у Kubernetes Service gateway відкриває порт 8080 пода (port-forward)
if (!publish) gateway.WithHttpEndpoint(port: 5000, name: "public");

builder.Build().Run();

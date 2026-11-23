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

// ---------- Сервіси: БД → сервіси → Aggregator → Gateway ----------
var loansApi = builder.AddProject<Projects.Library_Loans_Api>("loans-api")
    .WithReference(loansDb).WaitFor(loansDb)
    .WithHttpHealthCheck("/health");

var catalogApi = builder.AddProject<Projects.Library_Catalog_Api>("catalog-api")
    .WithReference(catalogDb).WaitFor(catalogDb)
    .WithHttpHealthCheck("/health");

var reviewsApi = builder.AddProject<Projects.Library_Reviews_WebApi>("reviews-api")
    .WithReference(reviewsDb).WaitFor(reviewsDb)
    .WithHttpHealthCheck("/health");

var aggregator = builder.AddProject<Projects.Platform_Aggregator>("aggregator")
    .WithReference(loansApi).WaitFor(loansApi)
    .WithReference(catalogApi).WaitFor(catalogApi)
    .WithReference(reviewsApi).WaitFor(reviewsApi)
    .WithHttpHealthCheck("/health");

// Єдина публічна точка входу з фіксованим портом 5000
builder.AddProject<Projects.Platform_ApiGateway>("gateway")
    .WithReference(aggregator).WaitFor(aggregator)
    .WithReference(loansApi).WithReference(catalogApi).WithReference(reviewsApi)
    .WithHttpEndpoint(port: 5000, name: "public")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

builder.Build().Run();

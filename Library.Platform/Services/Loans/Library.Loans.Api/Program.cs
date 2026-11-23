using Library.Loans.Api.Database;
using Platform.Security;
using Platform.Security.Authentication;
using Platform.Security.Authorization;
using AutoMapper;
using Library.Loans.Api.GrpcServices;
using Library.Loans.Bll;
using Library.Loans.Bll.Interfaces;
using Library.Loans.Bll.Services;
using Library.Loans.Bll.Telemetry;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Library.Loans.Dal;
using Library.Loans.Dal.Interfaces;
using Library.Loans.Domain;
using Library.Loans.Bll.Events;
using Platform.Messaging;
using Platform.Messaging.Events;
using Platform.Messaging.Topology;
using Platform.ServiceDefaults.Correlation;
using Platform.ServiceDefaults.GrpcSupport;

var builder = WebApplication.CreateBuilder(args);
builder.UseCleartextGrpcPort();   // Лаб. 10: h2c-порт gRPC у Kubernetes (Grpc:Cleartext=true)

// .NET Aspire: Serilog, OpenTelemetry, health checks, service discovery, resilience (Лаб. 05)
builder.AddServiceDefaults();

// DI: Unit of Work — connection string з конфігурації, з підтримкою environment variable override
var connectionString = builder.Configuration.GetConnectionString("LibraryLoans")
    ?? throw new InvalidOperationException("Connection string 'LibraryLoans' not found.");

builder.Services.AddScoped<IUnitOfWork>(_ => new UnitOfWork(connectionString));

// DI: BLL-сервіси
builder.Services.AddScoped<IReaderService, ReaderService>();
builder.Services.AddScoped<ILoanService, LoanService>();

// Лаб. 08: RabbitMQ (IConnection через Aspire, топологія при старті) і споживач черги loans.catalog-sync
builder.AddMessaging();
builder.Services.AddEventConsumer(MessagingTopology.LoansQueue, registry => registry
    .Handle<BookPriceChangedEvent, BookPriceChangedHandler>()
    .Handle<BookRenamedEvent, BookRenamedHandler>());

// Лаб. 07: бізнес-метрики (Singleton: один екземпляр Meter на весь час роботи сервісу)
builder.Services.AddSingleton<LoanMetrics>();

// Лаб. 07: health checks — SQL Server (критична залежність → Unhealthy), gRPC Health Checking Protocol
builder.Services.AddHealthChecks()
    .AddSqlServer(connectionString, name: "loans-db", failureStatus: HealthStatus.Unhealthy,
        tags: ["database", "sql", "ready"], timeout: TimeSpan.FromSeconds(5));
builder.Services.AddGrpcReadinessHealth();

// AutoMapper
builder.Services.AddAutoMapper(cfg => cfg.AddProfile<MappingProfile>());

// Лаб. 06: gRPC (серверний interceptor логує метод, peer, статус і тривалість)
builder.Services.AddGrpc(o => o.Interceptors.Add<GrpcLoggingInterceptor>());

// Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ProblemDetails — стандартизований формат помилок (RFC 7807/9457)
builder.Services.AddExceptionHandler<Library.Loans.Api.Middleware.GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Лаб. 09: JWT (Local/IdentityServer/Keycloak) + політики дозволів
builder.AddLibraryAuthentication(ApiResourceNames.Loans);
builder.Services.AddLibraryAuthorization();

var app = builder.Build();

// Лаб. 10: створення БД і схеми при старті в Kubernetes (в Aspire це робить WithCreationScript; скрипт ідемпотентний)
if (app.Configuration.GetValue<bool>("Database:InitializeOnStartup"))
    await LoansDatabaseInitializer.RunAsync(connectionString, app.Logger);

app.UseMiddleware<CorrelationIdMiddleware>();   // наскрізний X-Correlation-Id (ServiceDefaults)

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler(); // глобальна обробка через ProblemDetails-middleware (Крок 22)
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();   // у Development TLS не потрібен: сервіс слухає HTTP за Aspire/Gateway
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGrpcService<LoansGrpcService>();   // контракт Protos/loans.proto
app.MapGrpcReadinessHealth();             // grpc.health.v1.Health (Лаб. 07)
app.MapDefaultEndpoints();                // /health/live, /health/ready

app.Run();
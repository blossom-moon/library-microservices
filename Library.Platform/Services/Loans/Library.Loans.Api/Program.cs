using AutoMapper;
using Library.Loans.Bll;
using Library.Loans.Bll.Interfaces;
using Library.Loans.Bll.Services;
using Library.Loans.Dal;
using Library.Loans.Dal.Interfaces;
using Library.Loans.Domain;
using Platform.ServiceDefaults.Correlation;

var builder = WebApplication.CreateBuilder(args);

// .NET Aspire: Serilog, OpenTelemetry, health checks, service discovery, resilience (Лаб. 05)
builder.AddServiceDefaults();

// DI: Unit of Work — connection string з конфігурації, з підтримкою environment variable override
var connectionString = builder.Configuration.GetConnectionString("LibraryLoans")
    ?? throw new InvalidOperationException("Connection string 'LibraryLoans' not found.");

builder.Services.AddScoped<IUnitOfWork>(_ => new UnitOfWork(connectionString));

// DI: BLL-сервіси
builder.Services.AddScoped<IReaderService, ReaderService>();
builder.Services.AddScoped<ILoanService, LoanService>();

// AutoMapper
builder.Services.AddAutoMapper(cfg => cfg.AddProfile<MappingProfile>());

// Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ProblemDetails — стандартизований формат помилок (RFC 7807/9457)
builder.Services.AddExceptionHandler<Library.Loans.Api.Middleware.GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();
app.UseMiddleware<CorrelationIdMiddleware>();   // наскрізний X-Correlation-Id (ServiceDefaults)

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler(); // глобальна обробка через ProblemDetails-middleware (Крок 22)
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();   // у Development TLS не потрібен: сервіс слухає HTTP за Aspire/Gateway
app.UseAuthorization();
app.MapControllers();
app.MapDefaultEndpoints();   // /health і /alive

app.Run();
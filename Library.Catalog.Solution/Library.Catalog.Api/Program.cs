using FluentValidation;
using Library.Catalog.Api.Middleware;
using Library.Catalog.Bll;
using Library.Catalog.Bll.Interfaces;
using Library.Catalog.Bll.Services;
using Library.Catalog.Bll.Validators;
using Library.Catalog.Dal;
using Library.Catalog.Dal.Interfaces;
using Library.Catalog.Dal.Repositories;
using Library.Catalog.Dal.Seed;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// ---- Serilog: структуровані логи (console + file), SQL-запити EF Core на рівні Information ----
const string logTemplate =
    "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj} {Properties:j}{NewLine}{Exception}";

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: logTemplate)
    .WriteTo.File("logs/catalog-.log", rollingInterval: RollingInterval.Day, outputTemplate: logTemplate));

// ---- EF Core + Unit of Work ----
var connectionString = builder.Configuration.GetConnectionString("CatalogDb")
    ?? throw new InvalidOperationException("Connection string 'CatalogDb' not found.");

builder.Services.AddDbContext<CatalogDbContext>(o => o.UseSqlServer(connectionString));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// ---- BLL ----
builder.Services.AddScoped<IBookService, BookService>();
builder.Services.AddScoped<IAuthorService, AuthorService>();

// ---- AutoMapper (Community-ліцензія: ключ береться з конфігурації, якщо заданий) ----
var autoMapperKey = builder.Configuration["AutoMapper:LicenseKey"];
builder.Services.AddAutoMapper(cfg =>
{
    if (!string.IsNullOrWhiteSpace(autoMapperKey)) cfg.LicenseKey = autoMapperKey;
    cfg.AddProfile<MappingProfile>();
});

// ---- FluentValidation: реєстрація валідаторів; викликаються явно через IValidator<T> ----
builder.Services.AddValidatorsFromAssemblyContaining<CreateBookDtoValidator>();

// ---- API, Swagger, ProblemDetails ----
// Обов'язковість полів задається явно ([Required] на DTO), а не неявно через nullable-анотації
builder.Services.AddControllers(o => o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// ---- Міграції та ідемпотентний сідінг (вимикається в Production або Seeding:Enabled=false) ----
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    await db.Database.MigrateAsync();

    if (!app.Environment.IsProduction() && app.Configuration.GetValue("Seeding:Enabled", true))
        await DbInitializer.SeedAsync(db);
}

app.UseMiddleware<RequestContextMiddleware>();   // CorrelationId, UserId, RequestPath у LogContext
app.UseSerilogRequestLogging();
app.UseExceptionHandler();                       // глобальна обробка → ProblemDetails

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();

using Library.Reviews.Application;
using Library.Reviews.Application.Reviews;
using Library.Reviews.Infrastructure;
using Library.Reviews.WebApi.Middleware;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console());

var licenseKey = builder.Configuration["LuckyPenny:LicenseKey"];

builder.Services.AddApplication(licenseKey);
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddAutoMapper(cfg =>
{
    if (!string.IsNullOrWhiteSpace(licenseKey)) cfg.LicenseKey = licenseKey;
}, typeof(ReviewProfile).Assembly, typeof(Program).Assembly);

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(o => o.SuppressMapClientErrors = false);
builder.Services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(o =>
    o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseExceptionHandler();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

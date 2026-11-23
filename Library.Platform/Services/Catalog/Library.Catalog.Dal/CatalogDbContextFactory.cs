using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Library.Catalog.Dal;

/// <summary>
/// Використовується лише інструментом dotnet-ef (migrations add / script / database update).
/// У роботі застосунку рядок підключення береться з appsettings.json проєкту Api.
/// Перевизначити можна змінною середовища ConnectionStrings__CatalogDb.
/// </summary>
public class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    private const string DefaultConnection =
        @"Server=localhost\SQLEXPRESS;Database=Library_Catalog_Lab03;Trusted_Connection=True;TrustServerCertificate=True;";

    public CatalogDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__CatalogDb") ?? DefaultConnection;
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlServer(connection)
            .Options;
        return new CatalogDbContext(options);
    }
}

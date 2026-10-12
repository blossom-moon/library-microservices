var builder = DistributedApplication.CreateBuilder(args);

// Рядок підключення береться з appsettings.json цього проєкту
// (секція ConnectionStrings, ключ "LibraryLoans").
var loansDb = builder.AddConnectionString("LibraryLoans");

// API отримує рядок підключення від AppHost через WithReference —
// як змінну середовища ConnectionStrings__LibraryLoans, яку Program.cs
// в Library.Loans.Api читає через GetConnectionString("LibraryLoans").
builder.AddProject<Projects.Library_Loans_Api>("loans-api")
       .WithReference(loansDb);

builder.Build().Run();

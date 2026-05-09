# Bestgen / LedgerFlow MVC

MVP accounting and inventory management web application for Saudi SMEs.

## Stack

- ASP.NET Core MVC / Razor Views
- C# / Entity Framework Core Code First
- SQL Server
- ASP.NET Core Identity roles and users
- Bootstrap 5 RTL UI
- Chart.js dashboard charts

## Local Setup

1. Install .NET 8 SDK and SQL Server.
2. Update `appsettings.json` if your SQL Server connection differs.
3. Restore and run:

```bash
dotnet restore
dotnet run
```

The app seeds demo data on first run. For a migration-based workflow:

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

## Default Admin

- Email: `admin@ledgerflow.local`
- Password: `Admin@12345`

## Notes

Arabic RTL is the default UI. English/LTR and full resource localization are prepared structurally and can be expanded from the `Resources` folder.
# bestgen
# bestgen

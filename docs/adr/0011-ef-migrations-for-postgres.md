# ADR-0011: EF Core migrations for Postgres; EnsureCreated for SQLite dev

- **Status**: Accepted
- **Date**: 2026-05-10
- **Supersedes**: ADR-0009

## Context
ADR-0009 used `EnsureCreatedAsync` for both providers because the schema was
churning daily. Now we have ~70 entities and real customers are next — schema
changes can't keep wiping data.

Going to migrations is non-trivial because Bestgen runs on **two providers**
(SQLite locally, Postgres in production). Two options:

1. **Single migration set, both providers** — EF Core supports it but the
   first migration generated for one provider may not apply to the other
   (column types differ, e.g. `text` vs `TEXT`, `timestamp with time zone`
   vs `TEXT` for `DateTime`).
2. **Mixed**: production uses migrations, local dev keeps `EnsureCreatedAsync`
   for the fast `rm bestgen.db; dotnet run` loop.

## Decision
Option (2). The migration files target Postgres exclusively (generated with
`DatabaseProvider=Postgres ASPNETCORE_ENVIRONMENT=Production dotnet ef migrations add ...`).
SQLite local dev still uses `EnsureCreatedAsync`.

`DbSeeder.SeedAsync` picks the path automatically:

```csharp
var isPostgres = context.Database.ProviderName?.Contains("Npgsql") == true;
if (isPostgres && context.Database.GetMigrations().Any())
{
    // Bootstrap path: an existing EnsureCreated-built DB has tables but no
    // __EFMigrationsHistory. Insert the InitialCreate marker so MigrateAsync
    // skips the no-op InitialCreate run and applies anything newer.
    var creator = context.GetService<IRelationalDatabaseCreator>();
    var hasTables = await creator.ExistsAsync() && await creator.HasTablesAsync();
    var applied = await context.Database.GetAppliedMigrationsAsync();
    if (hasTables && !applied.Any())
    {
        await context.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (...);");
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"__EFMigrationsHistory\" VALUES ('<InitialCreate>', '8.0.5') ON CONFLICT DO NOTHING;");
    }
    await context.Database.MigrateAsync();
}
else
{
    await context.Database.EnsureCreatedAsync();
}
```

## Consequences
- Production deploys can change the schema without losing data.
- Adding a column = `dotnet ef migrations add AddColumnX` + commit. The deploy
  applies it on startup.
- Local SQLite dev workflow unchanged: change a model → `rm bestgen.db; dotnet run`.
- Migration files live in `Migrations/` — keep them in version control.
- The Initial migration (≈14k lines) was generated on 2026-05-10 and reflects
  every entity that existed at that moment.

## Adding a new migration

```bash
DatabaseProvider=Postgres \
ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=design;Username=design;Password=design" \
SeedDatabase=false \
ASPNETCORE_ENVIRONMENT=Production \
dotnet ef migrations add AddSomethingX --project Bestgen.csproj --output-dir Migrations
```

The connection string doesn't need to be reachable — EF only uses it for
SQL generation, not actual connections.

## Alternatives
- Provider-aware single migration set — rejected; would force every column
  declaration to handle both quirks.

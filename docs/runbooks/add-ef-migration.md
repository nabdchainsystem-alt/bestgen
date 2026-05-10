# Runbook: Add a new EF migration

Use this whenever you change an entity model — adding a column, table, index,
or rename. Production Postgres needs the migration; local SQLite re-creates
from the model.

## 1. Make the model change

Edit your entity in `Models/`. Build the project to make sure it still
compiles:

```bash
dotnet build Bestgen.sln
```

## 2. Generate the migration

The migration must target Postgres (see ADR-0011). The env vars below force
the design-time DbContext to use Npgsql even though your local dev uses SQLite:

```bash
DatabaseProvider=Postgres \
ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=design;Username=design;Password=design" \
SeedDatabase=false \
ASPNETCORE_ENVIRONMENT=Production \
dotnet ef migrations add Add<MyChangeName> --project Bestgen.csproj --output-dir Migrations
```

The connection string is fake — EF only uses it for SQL generation. No DB
needs to be running.

This creates two new files in `Migrations/`:

- `<timestamp>_Add<MyChangeName>.cs` — the up/down SQL
- `<timestamp>_Add<MyChangeName>.Designer.cs` — model snapshot

…and updates `ApplicationDbContextModelSnapshot.cs`.

## 3. Review the SQL

Open the new `.cs` file and read the `Up` method. Look for:

- **Bad data risk**: `DropColumn`, `RenameColumn`. EF doesn't preserve data
  across these — use `AddColumn` + manual data migration if you need to keep it.
- **Long locks**: adding a column with a default on a large table can lock for
  minutes on Postgres. Use a nullable column + backfill in a follow-up.
- **Missing index**: if you added a foreign key, EF creates one — but check.

## 4. Apply locally (Postgres only)

If you have a local Postgres for testing:

```bash
DatabaseProvider=Postgres \
ConnectionStrings__DefaultConnection="Host=localhost;...;Database=bestgen_test" \
dotnet run
```

…or just commit and let Render apply it on the next deploy.

## 5. Local SQLite dev: still rm + run

Migrations don't apply to SQLite (ADR-0011). For local iteration:

```bash
rm bestgen.db
dotnet run
```

## 6. Roll back

```bash
DatabaseProvider=Postgres ConnectionStrings__DefaultConnection="..." \
dotnet ef migrations remove --project Bestgen.csproj
```

This deletes the latest migration only. To roll back further use
`database update <PriorMigrationName>`.

## 7. Squash a wall of WIP migrations before merging

If you generated 5 migrations while iterating, squash to one before the PR:

```bash
# Remove all unapplied migrations, then add a single combined one
for i in {1..5}; do dotnet ef migrations remove --project Bestgen.csproj; done
dotnet ef migrations add ChangeXFinal --project Bestgen.csproj --output-dir Migrations
```

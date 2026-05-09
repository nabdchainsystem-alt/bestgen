---
name: reset-db
description: Reset the Bestgen SQLite database so schema changes take effect. Use whenever the user changes an entity model, adds a new entity, adds an enum to an existing column, or asks to "reset the database", "rebuild the DB", "reseed", "clear data", or hits a "no such column" / "no such table" error at runtime.
---

# Reset the Bestgen database

Bestgen has **no EF migrations**. Schema is created once via
`EnsureCreatedAsync()` in `Program.cs` → `DbSeeder.SeedAsync`. That call only
creates tables when the file is missing — it never alters an existing schema.

So **any model change requires deleting the DB file and letting it regenerate**.

## When this is needed

- Added a new entity / `DbSet<>`.
- Added/removed/renamed a column on an existing entity.
- Changed a column type (string → enum, int → decimal, nullable change).
- Added a new enum to an existing column.
- Got a SQLite error like `no such column: <X>` or `no such table: <X>`.

Pure code-only changes (controllers, views, services, helpers, CSS) do **not**
need a reset.

## The command

```bash
rm bestgen.db
dotnet run
```

That's it. On first run, `DbSeeder` recreates the schema and reseeds:
- Roles: `Owner, Admin, Accountant, Sales, Purchases, Warehouse, HR, Cashier, Viewer`
- Admin users (passwords reset every run, even on existing DB):
  - `max@bestgen.com` / `123`
  - `admin@ledgerflow.local` / `Admin@12345`
- Sample chart of accounts, demo customers/suppliers/products, etc.

## Verify

```bash
ls -la bestgen.db   # exists, recent timestamp
```

Then log in with `max@bestgen.com` / `123` at `http://localhost:5000`.

## Don'ts

- Don't introduce EF migrations to "fix" schema drift — it's not the
  project's chosen workflow. The friction of `rm bestgen.db` is intentional;
  the dev DB is throwaway.
- Don't delete `bestgen.db-journal` / `bestgen.db-wal` / `bestgen.db-shm` by
  hand — SQLite manages them.
- If running uncovers a seed-time error (e.g. duplicate key), fix `DbSeeder`,
  delete the DB again, re-run. Don't comment out the failing seed.
- Never delete the DB on a deployed/staging environment without an explicit
  ask. This skill is for local dev.

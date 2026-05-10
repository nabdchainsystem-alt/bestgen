# Runbook: Database schema drift / "no such column / no such table"

**Symptom**: Runtime exception `SqliteException: no such column "X"` or
`relation "Y" does not exist` (Postgres). New entity model doesn't match the
existing schema.

This is a known consequence of [ADR-0009](../adr/0009-ensurecreated-not-migrations.md)
— while we're on `EnsureCreatedAsync`, every model change requires re-creating
the schema.

## Local dev (SQLite)

```bash
rm bestgen.db bestgen.db-shm bestgen.db-wal
dotnet run
```

This re-runs `EnsureCreatedAsync` + `DbSeeder` so all the demo data
(customers, products, accounts, currencies, FX rates) returns.

If you want to keep the data: backup first.

```bash
cp bestgen.db bestgen.db.backup-$(date +%Y%m%d-%H%M%S)
```

## Production (Render Postgres)

```sql
-- In Render's Postgres console:
DROP SCHEMA public CASCADE;
CREATE SCHEMA public;
GRANT ALL ON SCHEMA public TO public;
```

Then redeploy the service so `EnsureCreatedAsync` runs. **All tenant data is
lost.** Take a `pg_dump` first if there's anything you can't lose:

```bash
pg_dump $DATABASE_URL > /tmp/bestgen-pre-drift-$(date +%F).sql
```

## After the migration story is in place

Once ADR-0009 is reversed (we ship `dotnet ef migrations add`), this runbook
becomes "run `Database.MigrateAsync()` on deploy" — schema changes apply
incrementally and never destroy data.

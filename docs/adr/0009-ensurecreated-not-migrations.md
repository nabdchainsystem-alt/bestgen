# ADR-0009: `EnsureCreatedAsync` instead of EF migrations (initial phase)

- **Status**: Superseded by ADR-0011 — Postgres now uses migrations
- **Date**: 2026-04-10
- **Superseded**: 2026-05-10

## Context
EF Core gives two ways to create the schema:

1. `Database.EnsureCreatedAsync()` — idempotent, no migration history.
2. `Database.MigrateAsync()` with checked-in migrations.

While entity models are still churning rapidly, every model change with (2)
requires `dotnet ef migrations add ...` and a code review. With (1) the dev
loop is `rm bestgen.db; dotnet run` — much faster.

## Decision
Use `EnsureCreatedAsync` for now. Document in CLAUDE.md that schema changes
need `rm bestgen.db` (dev) or `DROP SCHEMA public CASCADE` (Render Postgres).

## Consequences
- Fast iteration during the build-out phase.
- **Cannot ship to real customers as-is** — schema changes would wipe their
  data.
- No migration history → no rollback story.
- Auditing schema changes over time = `git log` of model files only.

## Reversal plan
1. Generate an initial migration that captures the current schema:
   `dotnet ef migrations add InitialCreate`.
2. Mark it as applied on existing databases:
   `INSERT INTO __EFMigrationsHistory VALUES ('20260101_InitialCreate', '8.0.5');`
3. Replace `EnsureCreatedAsync` call with `MigrateAsync`.
4. From then on every model change ships a migration.

## Alternatives
- Migrations from day one — rejected; would have slowed initial build.

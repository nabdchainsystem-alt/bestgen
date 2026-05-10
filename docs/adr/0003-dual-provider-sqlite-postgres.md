# ADR-0003: Dual EF Core provider — SQLite locally, Postgres in production

- **Status**: Accepted
- **Date**: 2026-04-22

## Context
Local development should be zero-setup (no DB server needed). Production needs
a managed DB with backups, replicas, and Render's free Postgres tier is the
cheapest hosting option.

## Decision
A `DatabaseProvider` config key (`Sqlite` default, `Postgres` in Render env)
switches the EF Core provider at startup. The connection string flows in via
`ConnectionStrings:DefaultConnection`. Render passes the postgres:// URL which
we normalize to Npgsql key=value form in `Program.cs`.

`Npgsql.EnableLegacyTimestampBehavior=true` is set before any Npgsql type
loads so `DateTime.Today` (Kind=Unspecified) maps to `timestamp without time
zone` instead of being rejected as non-UTC.

## Consequences
- Same code, two databases — schema must avoid provider-specific quirks
  (`ORDER BY decimal` was caught after it shipped — see ADR-0010).
- `EF.Functions.DateDiffDay` is SQL-Server-specific and would break here; use
  `(date1 - date2).TotalDays` after pulling rows.
- Hangfire storage also picks per provider (memory in dev, postgres in prod).
- Migrations need to run on both — slows the migration story slightly.

## Alternatives
- Postgres locally — rejected; adds setup friction for new contributors.
- LiteDB or in-memory — rejected; doesn't model SQL accurately enough to catch
  prod issues at dev time.

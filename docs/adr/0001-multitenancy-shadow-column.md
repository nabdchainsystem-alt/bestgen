# ADR-0001: Multi-tenancy via shadow TenantId column

- **Status**: Accepted
- **Date**: 2026-04-15

## Context
Bestgen needs to host multiple companies in one deployment so we can sell on
shared infrastructure. Three options were considered:

1. Database-per-tenant — strongest isolation, hardest ops (one migration per tenant).
2. Schema-per-tenant on Postgres — better than (1) but doesn't fit our SQLite local dev.
3. Shared schema + shadow TenantId column with EF Core query filters.

## Decision
Adopt option (3). Every business entity (anything in `bestgen.Models`, except
`Tenant` and `ApplicationUser`) gets a shadow `TenantId int` column applied via
reflection in `ApplicationDbContext.OnModelCreating`. A query filter scopes
every read to the current tenant id, and a SaveChanges interceptor stamps it on
every insert.

`ApplicationUser.CurrentTenantId` selects the active tenant. A custom
`UserClaimsPrincipalFactory` injects it as a `tenant_id` claim so per-request
code (auth, audit log, structured logs) can read it without DB roundtrips.

## Consequences
- One DB, one connection pool, one migration story.
- Cross-tenant queries are impossible by accident — query filter blocks them.
- Tenant deletion is cheap (`DELETE WHERE TenantId=N`), backup is cheap, but
  noisy-neighbor risk is real on Postgres → must monitor query plans.
- Adding a new entity automatically gets tenant-scoped — no per-entity wiring.

## Alternatives
- Separate DBs per tenant — ruled out because Render free tier limits us to one
  DB and managing dozens of migrations is hostile for a small team.

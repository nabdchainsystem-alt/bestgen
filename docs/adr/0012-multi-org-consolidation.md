# ADR-0012: Multi-org consolidation; inter-company sync stubbed

- **Status**: Accepted (F4 done; F5 schema-only)
- **Date**: 2026-05-10

## Context
Holding companies want consolidated reporting across subsidiaries (each
subsidiary = one Bestgen Tenant) and want sales between subsidiaries to
auto-mirror as purchases on the counterpart side.

## Decision

**F4 — Consolidation**: ship completely.
- New `Organization` entity with a unique slug.
- `Tenant.OrganizationId` (nullable) — points at the parent group.
- `ConsolidationService` aggregates revenue / VAT / outstanding receivables
  / cash in / cash out across every Tenant in the org. Reads use
  `IgnoreQueryFilters()` plus a manual `EF.Property<int>(x, "TenantId") IN (…)`
  filter so a user can never see data from a tenant outside their org.
- `/Consolidation` page with a date range, summary cards, and per-workspace
  breakdown table.

**F5 — Inter-company sync**: schema only.
- `Customer.LinkedTenantId` (nullable) — when set, this Customer represents
  another Tenant in the same Organization.
- The actual cross-tenant write path (mirror Sales→Purchase across DbContext
  scopes) is **intentionally not enabled** in this iteration. Cross-tenant
  `SaveChanges` requires:
  - Either bypassing `TenantSaveChangesInterceptor` for one save, then
    flipping back, which is fragile; or
  - Spinning a fresh `DbContext` per target tenant, which doubles the
    transaction story, or
  - Hangfire job with admin-token impersonation.
- Each path needs careful testing and at least one product decision around
  conflict policy (what if the target tenant rejects the SKU?).

## Consequences
- Today: a holding company can install Bestgen, set `Tenant.OrganizationId`
  on each subsidiary, and view consolidated metrics immediately.
- A `LinkedTenantId` field exists for the eventual F5 wiring without another
  migration.
- The consolidation page deliberately uses simple SQL (no EF query filter
  bypass smell anywhere else in the code) so any future query optimization
  is easy.

## When we wire F5

The cleanest path:

1. Hangfire job `MirrorSalesInvoiceJob(int sourceInvoiceId)`.
2. Job opens a fresh scope, manually sets `ITenantContext.TenantId` to the
   target tenant id (already supported via the existing claim plumbing).
3. Resolves a Supplier by `LinkedTenantId == sourceTenantId` (or creates one).
4. Resolves Products by SKU (or creates a "Cross-org sale" placeholder).
5. Calls `PurchaseInvoiceService.CreateAsync` normally — query filter +
   audit interceptor handle the rest.
6. Idempotency: tag the source `SalesInvoice.Notes` with the mirrored id;
   if the tag is present on retry, no-op.

## Alternatives considered
- Materialized views on Postgres for consolidated reads — rejected; we hit
  zero-tenant-leak via row-level filter, materialized views would be a
  second source of truth.
- Separate "consolidation database" — rejected; doubles the ops story.

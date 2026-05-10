# ADR-0006: Hangfire for background jobs (replaces BackgroundService)

- **Status**: Accepted
- **Date**: 2026-05-10

## Context
Initially `RecurringInvoiceHostedService` was a plain `BackgroundService` that
woke hourly and ran due recurring invoices. As the app grew (delivery,
ZATCA submit, idempotency cleanup), the gaps became:

- No retries on failure.
- No persistence — restart loses scheduled work.
- Multi-instance unsafe — every Render replica fires the same job.
- No visibility into what ran and when.

## Decision
Adopt **Hangfire** with provider-aware storage:

- `Hangfire.MemoryStorage` for SQLite local dev (jobs ephemeral, fine for dev).
- `Hangfire.PostgreSql` in production (auto-creates `hangfire` schema).

Recurring jobs:
- `recurring-invoice-tick` — `Cron.Hourly()` → `RecurringInvoiceService.RunDueAsync`.
- `idempotency-cleanup` — `0 */6 * * *` → trim expired idempotency keys.

The dashboard at `/hangfire` is gated by `HangfireAuthFilter` (Owner/Admin
only).

## Consequences
- Distributed locks → only one replica runs each recurring job.
- Retries are automatic + visible.
- Schema dependency in prod (Postgres); fine, we already use Postgres there.
- Slight startup cost (Hangfire schema bootstrap + worker thread).

## Alternatives
- Quartz.NET — heavier API, less polish on the dashboard.
- Custom `BackgroundService` with leader election — possible but reinvents the
  wheel; not worth ~1k LoC.

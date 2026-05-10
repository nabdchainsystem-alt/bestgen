# Runbook: Recurring invoices not generating

**Symptom**: A recurring template's `NextRunDate` is in the past but no
invoice was created.

## 1. Open the Hangfire dashboard

Sign in as Owner/Admin → `/hangfire`.

## 2. Check `Recurring Jobs`

Find `recurring-invoice-tick`:

| State | Action |
|---|---|
| Last execution Succeeded | The job ran but found 0 due templates. Check `RecurringInvoices.NextRunDate` and `IsActive`. |
| Last execution Failed | Click the job → see the exception in Sentry / Hangfire logs. Common: `SalesInvoiceService.CreateAsync` failed because the template's CustomerId/ProductId was deleted. |
| No last execution | Hangfire server isn't running. Restart the app. |

## 3. Manual trigger

In the dashboard: **Recurring Jobs → recurring-invoice-tick → Trigger now**.

Or from the UI: `/RecurringInvoices` → table row → **Run now** button.

## 4. Disable a broken template

If a template keeps failing (deleted product, customer in archive, etc.):
edit it and uncheck **Is Active**, or delete it.

## 5. Multi-instance gotcha

In production, Hangfire's distributed lock ensures only one replica runs the
job. In dev with multiple `dotnet run` processes, every process runs it — fine
for dev but can produce duplicates. Stop extra processes before testing.

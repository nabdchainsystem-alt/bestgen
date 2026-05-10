# Runbook: Restore a single tenant from backup

**When**: A user accidentally deleted their data, or a bug corrupted one
tenant's records and others are fine.

## 1. Confirm scope

```sql
-- How much data does this tenant have?
SELECT 'SalesInvoices', COUNT(*) FROM "SalesInvoices" WHERE "TenantId" = $1
UNION ALL SELECT 'PurchaseInvoices', COUNT(*) FROM "PurchaseInvoices" WHERE "TenantId" = $1
UNION ALL SELECT 'Customers', COUNT(*) FROM "Customers" WHERE "TenantId" = $1
UNION ALL SELECT 'Products', COUNT(*) FROM "Products" WHERE "TenantId" = $1;
```

## 2. Find a clean backup

Render free tier: no automatic backups — hopefully you took a `pg_dump`
manually. If not, this is a recovery story without source-of-truth.

Render paid: latest automatic backup is a known timestamp.

## 3. Restore approach (other tenants must keep their fresh data)

You **cannot** just `pg_restore` over the whole DB — that wipes other
tenants' updates since the backup.

Two-step pattern:

```bash
# 1. Restore the backup into a temp database
createdb bestgen_restore
pg_restore -d bestgen_restore /path/to/backup.dump

# 2. Copy ONLY this tenant's rows from the temp DB into prod
TENANT_ID=42
for table in Customers Suppliers Products Accounts SalesInvoices SalesInvoiceItems \
             PurchaseInvoices PurchaseInvoiceItems JournalEntries JournalEntryLines \
             ApprovalRequests Attachments InvoiceDeliveryLogs; do
  pg_dump -d bestgen_restore -t "\"$table\"" --data-only \
    --where="\"TenantId\" = $TENANT_ID" | psql $DATABASE_URL
done
```

## 4. Verify

Login as a user from that tenant. Spot-check:
- Customer list count matches the backup time.
- Recent invoices are present.
- The audit log shows the corrupting event so you understand what happened.

## 5. Postmortem

Document the corruption cause in `docs/incidents/YYYY-MM-DD-tenant-NN.md`.
Add a regression guard if appropriate.

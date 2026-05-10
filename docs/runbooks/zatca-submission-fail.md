# Runbook: ZATCA submission failed

**Symptom**: Invoice details page shows ZATCA status "Failed". `FatooraResponse`
column on the `EInvoices` table contains an HTTP error body. Sentry alert
"ZATCA submission failed".

## 1. Identify the failure mode

Look at `FatooraResponse`:

| Response shape | Likely cause |
|---|---|
| `{ "status": "stub", "note": "FATOORA not configured..." }` | Sandbox/dev mode — set `Fatoora:BinarySecurityToken` + `Fatoora:Secret` |
| `{"errors":[{"code":"BR-...","category":"...","message":"..."}]}` | UBL validation error — invoice fields don't match ZATCA business rules |
| `{"errors":[{"code":"AUTH-..."}]}` | CSID/secret expired or wrong mode (Sandbox vs Production) |
| `HTTP 5xx` | ZATCA gateway transient — automatic retry via Hangfire |

## 2. UBL validation errors (BR-* / KSA-*)

1. Open `/SalesInvoices/Details/{id}` → click `XML` to download the generated UBL.
2. Compare against ZATCA's [validation rule list](https://zatca.gov.sa/en/E-Invoicing/SystemsDevelopers/Pages/default.aspx).
3. Fix the missing/incorrect field (usually customer VAT number, address, or invoice line VAT category).
4. **Don't** click Generate again on the same invoice — it'll reuse the existing EInvoice. To force regenerate:
   ```sql
   DELETE FROM EInvoices WHERE SalesInvoiceId = <id>;
   ```
   Then click Generate, then Submit.

## 3. CSID / auth issues

Check Render env vars:
- `Fatoora__Mode` matches the CSID's environment (Sandbox CSIDs only work in Sandbox).
- `Fatoora__BinarySecurityToken` is the base64 string from the onboarding response.
- `Fatoora__Secret` is the password from the same response.

If the CSID expired (production CSIDs rotate), re-onboard via ZATCA's portal.

## 4. Transient gateway errors

Hangfire automatically retries failed `SubmitAsync` jobs up to 10 times with
exponential backoff. Check `/hangfire/jobs/failed` — manually requeue from the
dashboard if you've fixed the root cause.

## 5. Verify

After fixing, on a single invoice:
1. Click **Generate E-Invoice** (or skip if EInvoice row already exists).
2. Click **Submit**.
3. `Status` should flip to `Cleared` (B2B) or `Reported` (B2C).
4. The `Cleared` invoice's XML is replaced with the ZATCA-signed copy — verify the new `<UBLExtensions>` block is present.

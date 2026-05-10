# Runbook: Users hit HTTP 429 Too Many Requests

**Symptom**: User's browser shows a "Too many requests" page or a JSON error
on `/SalesInvoices/Send`, `/SubmitZatca`, or `/Identity/Account/Login`.

## 1. Identify which limiter fired

Limiter policies are defined in `Program.cs`:

| Policy | Window | Per-IP limit | Endpoints |
|---|---|---|---|
| `login` | 5 min | 10 | (planned) `/Identity/Account/Login` |
| `delivery` | 1 min | 30 | `/SalesInvoices/Send`, `/PurchaseInvoices/Send` |
| `zatca` | 1 min | 60 | `/SalesInvoices/SubmitZatca` |

A burst beyond the limit returns 429 immediately.

## 2. Legitimate user blocked?

- They have a static IP and several team members behind a NAT — the per-IP
  bucket fills fast. Solution: switch policy to per-user instead of per-IP
  (key by `User.Identity.Name`). Mark in `Program.cs` for next iteration.

## 3. Attack pattern?

Check Sentry / log volume on the same IP. If it's sustained, leave the limiter
to do its job. If it's spammy enough to need permanent block, add the IP to
your edge proxy / Cloudflare / Render IP allow list.

## 4. Tuning

Edit `Program.cs` → `AddRateLimiter` → bump the per-policy `PermitLimit`.
Redeploy. Apply only after confirming the legitimate use case.

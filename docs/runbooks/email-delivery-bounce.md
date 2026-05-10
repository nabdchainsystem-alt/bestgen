# Runbook: Email delivery failing / bouncing

**Symptom**: Invoice details → Delivery tab shows red "Failed" rows. Customers
report not receiving invoices.

## 1. Confirm scope

Hit `/Audit?action=Update&entity=InvoiceDeliveryLog` (or query directly):

```sql
SELECT Channel, Status, COUNT(*) FROM InvoiceDeliveryLogs
WHERE SentAt > now() - INTERVAL '24 hours'
GROUP BY Channel, Status;
```

If 100% Email failing → SMTP config or provider down. If only some → bounces
on specific recipient domains.

## 2. SMTP config check

Render → Environment → confirm:
- `Smtp__Host` (e.g. `smtp.sendgrid.net`)
- `Smtp__Port` (587 for TLS, 465 for SSL — match your provider)
- `Smtp__User`, `Smtp__Password` set
- `Smtp__FromEmail` is a domain you've **verified** with the SMTP provider — Gmail/Outlook receivers reject mail from unverified senders silently.

## 3. Check the actual error

Each failed `InvoiceDeliveryLog` row stores the SMTP error in `ErrorMessage`:

| Snippet | Meaning |
|---|---|
| `Authentication failed` | bad user/password — rotate the API key with the provider |
| `relay access denied` | `From` not verified for this account |
| `554 ... blocked` | provider flagged content/IP — open a ticket with them |
| `connection timed out` | wrong host/port or Render egress is being blocked |

## 4. Bounce monitoring

If using SendGrid/Postmark, set up a webhook → store bounces. Bestgen doesn't
do this yet (todo). For now, watch the provider dashboard.

## 5. Manual retry

Open the invoice → **Send** → re-enter the recipient email. The Delivery
tab will record the fresh attempt with whatever error the provider returns.

## 6. Bypass for urgent cases

If SMTP is down, use **Send by WhatsApp** instead, or download the PDF and
email manually from your own client.

# Bestgen ERP

Arabic-first accounting + inventory SaaS for the Saudi market. Multi-tenant,
ZATCA Phase 2 ready, with built-in POS, recurring billing, AI invoice
scanning, and bilingual (Arabic + English) PDFs.

> Status: dev / pre-customer. See [docs/adr/0009](docs/adr/0009-ensurecreated-not-migrations.md)
> before changing entity models.

## Stack

- **ASP.NET Core MVC, .NET 8**
- **Entity Framework Core 8** — SQLite locally, Postgres in production
- **ASP.NET Identity** with custom tenant claims
- **QuestPDF + Cairo TTF** for bilingual invoice PDFs
- **Hangfire** for background jobs (memory storage in dev, Postgres in prod)
- **Serilog** structured logging + Sentry for error monitoring
- **Prometheus** for metrics, exposed at `/metrics`
- **Bootstrap 5 RTL** with a custom RTL sidebar layout

## Quick start

Requires .NET 8 SDK.

```bash
git clone <this repo>
cd bestgen
dotnet restore
dotnet run
```

Open http://localhost:5000 and log in:

| Email | Password | Roles |
|---|---|---|
| `max@bestgen.com` | `123` | Owner, Admin |
| `sam@bestgen.com` | `123` | Owner, Admin |
| `badwy@bestgen.com` | `123` | Owner, Admin |
| `admin@ledgerflow.local` | `Admin@12345` | Owner, Admin (legacy) |

Sample customers, suppliers, products, accounts, currencies, and FX rates are
seeded on first run.

## Day-2 commands

```bash
# Wipe local DB after changing entity models (see ADR-0009)
rm bestgen.db
dotnet run

# Hangfire dashboard (Owner/Admin only)
open http://localhost:5000/hangfire

# Metrics scrape endpoint
curl http://localhost:5000/metrics

# Health checks
curl http://localhost:5000/health/live
curl http://localhost:5000/health/ready

# Run tests
dotnet test Bestgen.sln
```

## Deployment (Render)

`render.yaml` declares a Docker web service + free Postgres. Connect the repo
in Render and pick "Blueprint" — it'll set up both. After the first deploy:

1. Render Dashboard → **Environment** — set the secrets that ship with
   `sync: false`:
   - `ANTHROPIC_API_KEY` (AI invoice scanning)
   - `Smtp__Host`, `Smtp__User`, `Smtp__Password`, `Smtp__FromEmail`
   - `WhatsApp__AccessToken`, `WhatsApp__PhoneNumberId`
   - `Fatoora__BinarySecurityToken`, `Fatoora__Secret`
   - `Sentry__Dsn`
2. **Manual Deploy → Clear build cache & Deploy** to apply env vars.
3. Open the public URL → log in with seeded creds → upload your logo at
   `/Settings/Edit`.

See [docs/runbooks/render-redeploy.md](docs/runbooks/render-redeploy.md) for
the full deploy + rollback playbook.

## Configuration reference

All keys read from `appsettings.json` and overridable via env vars in
double-underscore form (e.g. `Smtp__Host` → `Smtp:Host`).

| Section | Purpose |
|---|---|
| `ConnectionStrings:DefaultConnection` | DB connection string |
| `DatabaseProvider` | `Sqlite` (default) or `Postgres` |
| `SeedDatabase` | `true` to run `DbSeeder` on startup |
| `Ai:AnthropicApiKey`, `Ai:Model` | Claude API for invoice scanning |
| `Smtp:*` | Outbound email |
| `WhatsApp:*` | Meta Cloud API for WhatsApp invoice delivery |
| `Fatoora:*` | ZATCA FATOORA submission |
| `Sentry:Dsn`, `Sentry:TracesSampleRate` | Error monitoring |
| `Serilog:MinimumLevel` | Per-namespace log level overrides |

## Architecture overview

| Concern | Pattern | Where |
|---|---|---|
| Multi-tenancy | Shadow `TenantId` column + EF query filter | `Data/ApplicationDbContext.cs` |
| Generic CRUD | `CrudController<T>` + reflective views | `Controllers/CrudController.cs`, `Views/Shared/Crud/*` |
| Custom forms | Hand-rolled controller + ViewModel + `_Form.cshtml` | `Controllers/SalesInvoicesController.cs`, etc. |
| Background jobs | Hangfire recurring | `Program.cs` |
| Idempotency | `[Idempotent]` action filter + `IdempotencyKey` table | `Services/Idempotency/*` |
| Audit trail | `AuditSaveChangesInterceptor` captures every write | `Data/AuditSaveChangesInterceptor.cs` |
| Approvals | `ApprovalPolicy` chain + `ApprovalRequest` walks `CurrentStep` | `Services/ApprovalService.cs` |
| ZATCA | UBL builder → SHA-256 → ECDSA P-256 → 9-field TLV QR → submit | `Services/Zatca/*` |
| PDF | QuestPDF + Cairo bilingual TTF + ZATCA QR | `Services/InvoicePdf/*` |

## Observability

| Signal | Where |
|---|---|
| Logs | Serilog → Console (JSON in prod) → Sentry on warning+ |
| Errors | Sentry — set `Sentry__Dsn` |
| Metrics | Prometheus at `/metrics`. HTTP timing built-in; custom counters in `BestgenMetrics` |
| Health | `/health/live`, `/health/ready`, `/health` |
| Audit | `/Audit` UI on every write event |
| Job runs | `/hangfire` dashboard |

## Documentation

- [docs/adr/](docs/adr/) — architecture decisions (10 records, see ADR-0000 template)
- [docs/runbooks/](docs/runbooks/) — operational playbooks
- `CLAUDE.md` — conventions and module structure (also acts as the agent prompt)

## License

Proprietary. Bestgen is a closed-source commercial product. The
[Cairo font](https://github.com/Gue3bara/Cairo) is OFL.

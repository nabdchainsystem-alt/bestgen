# Bestgen — Claude Working Notes

Premium Arabic SaaS accounting + inventory web app. Use this file as the
source of truth for conventions, tech stack, and the module/menu layout.

## Tech stack (do NOT change)

- ASP.NET Core MVC, .NET 8 (`net8.0`)
- C# with `Nullable` and `ImplicitUsings` enabled
- Entity Framework Core 8 + **SQLite** (`bestgen.db`)
- Razor Views, Bootstrap 5 RTL (`bootstrap.rtl.min.css`), Bootstrap Icons
- Chart.js 4 for dashboard charts
- ASP.NET Core Identity with `ApplicationUser`
- Default culture `ar-SA`, RTL layout
- Root namespace: `bestgen` (lowercase). Assembly name: `Bestgen`.

Never switch to React/Next/Node/pnpm. Never change the target framework.
Never replace SQLite with another provider. Never remove Identity.

## Build / Run

```
dotnet clean
dotnet restore
dotnet build
dotnet run            # listens on http://localhost:5000 by default
```

Default admin (seeded by `SeedData/DbSeeder.cs`):

- primary login: `max@bestgen.com` / `123`
- legacy login (still seeded): `admin@ledgerflow.local` / `Admin@12345`
- roles: `Owner, Admin, Accountant, Sales, Purchases, Warehouse, HR, Cashier, Viewer`

Both accounts have `Owner` + `Admin` roles. The seeder resets their passwords on
every run so they always work, even on an existing DB. Identity password rules
in `Program.cs` are intentionally relaxed for local/dev — tighten them before
shipping.

### Database lifecycle

There are no EF migrations checked in. `Program.cs` runs `DbSeeder.SeedAsync`
which calls `EnsureCreatedAsync()` — that creates the schema only if the
database file is missing, so **changing the entity model requires deleting
`bestgen.db` and letting it regenerate**. When you add or change models:

```
rm bestgen.db
dotnet run
```

## Architecture

### Generic CRUD framework

Most simple modules use the generic CRUD framework — controllers inherit
`Controllers/CrudController<TEntity>` and views are auto-rendered from the
shared templates in `Views/Shared/Crud/{Index,Create,Edit,Details,Delete}.cshtml`.
Each new module typically only needs:

1. Entity class in `Models/`
2. `DbSet<>` registration in `Data/ApplicationDbContext.cs`
3. Helper metadata in `Helpers/EntityDisplayHelper.cs` (module title +
   description, list properties, Arabic labels)
4. A 5-line controller deriving from `CrudController<TEntity>` (override
   `Query()` if you need `.Include()` for related data)

`EntityDisplayHelper` controls what appears in lists, what the column labels
say, how money/dates/enums format, and which fields show in the auto-generated
form. New entities still render even if not registered, but you should add
proper labels and a `ListProperties` entry for each.

### Lookups

`CrudController.PopulateLookupsAsync` auto-fills dropdowns for any property
ending in `Id` (e.g. `WarehouseId`, `CustomerId`, `EmployeeId`,
`ProductCategoryId`, `AssetId`). Add new lookup branches there when adding
new FKs that should resolve to a select list.

### Hand-rolled controllers

Modules with complex multi-line forms have full custom controllers and views,
not the generic CRUD framework:

- `SalesInvoicesController` + `Views/SalesInvoices/`
- `PurchaseInvoicesController` + `Views/PurchaseInvoices/`
- `JournalEntriesController` + `Views/JournalEntries/`

These use form ViewModels in `ViewModels/` and services in `Services/`
(`SalesInvoiceService`, `PurchaseInvoiceService`, `InvoiceCalculationService`,
`InventoryService`, `AccountingService`).

### Services / business logic

Keep controllers thin. Business rules live in `Services/`:

- `InvoiceCalculationService` — front+back recalculation of totals/VAT
- `InventoryService` — stock movements; low-stock query
- `AccountingService` — journal entry generation, balance check, expense
  cash/bank deduction, paid-expense journal
- `SalesInvoiceService` / `PurchaseInvoiceService` — orchestrate calc +
  inventory + accounting on save
- `DashboardService` — aggregates KPIs and chart data
- `ReportService` — returns the report card list shown in `/Reports`

Add new services here (don't put logic in controllers) for HR finance,
fixed assets, supplier payments, etc.

### Layout & UI

`Views/Shared/_Layout.cshtml` is the master shell:

- right-side RTL sidebar with collapsible groups (uses Bootstrap collapse)
- sticky topbar with quick search, language chip, dark-mode chip, user menu
- main area with `.page-header`, `.metrics-grid`, `.dashboard-grid`,
  `.data-panel`, `.form-panel`, `.details-panel`, `.report-grid`

CSS lives in `wwwroot/css/site.css`. Color palette in `:root`:
`--canvas`, `--surface`, `--border`, `--text`, `--muted`, `--navy`,
`--emerald`, `--blue`, `--warning`, `--danger`. Stick to these — no
childish colors.

Reusable partials in `Views/Shared/`:

- `_PageHeader` — every page starts with this
- `_DashboardCard` — KPI cards
- `_StatusBadge` — colored badge resolved from `EntityDisplayHelper.StatusClass`
- `_SearchBar` — list-view filter (search + status + apply)
- `_EmptyState` — used by CRUD index when no rows
- `_ActionButtons` — view/edit/delete trio for a row
- `_ReportCard` — Reports Center tile

When you add a new entity-screen flow, prefer composing these partials over
inventing a new shape so visual identity stays consistent.

## Sidebar / module structure (canonical)

The right sidebar groups, in order. Each item must link to a working page;
empty modules render the generic CRUD index with empty-state messaging — no
dead links.

1. **لوحة التحكم** → `DashboardController`
2. **المبيعات** — Customers, SalesQuotations, SalesInvoices,
   SalesReceipts, SalesRefundReceipts, CreditNotes, DeliveryNotes,
   SalesPricePolicies
3. **المشتريات** — Suppliers, PurchaseOrders, PurchaseInvoices,
   SupplierPayments, PurchaseRefundReceipts, DebitNotes, GoodsReceipts,
   PurchasePricePolicies
4. **المنتجات والمخزون** — ProductCategories, Products, Warehouses,
   InventoryCounts, StockTransfers
5. **الأصول الثابتة** — FixedAssets, AssetTags, AssetRentals
6. **المحاسبة المتقدمة** — Accounts, JournalEntries, GeneralReceipts,
   OpeningBalances, ReportingDimensions
7. **الموارد البشرية** — Employees, EmployeeDocuments, PayrollEntries,
   EmployeeDeductions, EmployeeBonuses, EmployeeLoans, EmployeeReceipts,
   EmployeeRequests
8. **التقارير** → `ReportsController` (Reports Center)
9. **الإعدادات** → `SettingsController`

The Reports Center groups its cards into:
محاسبة · المبيعات · المشتريات · المنتجات والمخزون · الموارد البشرية —
keys are stable strings (e.g. `general-ledger`, `customer-statement`)
served via `Reports/Details?id=<key>`.

## Code conventions

- Use `decimal` for all money. `decimal` precision is configured at
  `(18, 2)` in `ApplicationDbContext.ConfigureConventions`.
- Use async EF (`ToListAsync`, `FirstOrDefaultAsync`, etc.).
- Use `ViewModels/` for forms with multiple lines or computed totals.
- Always include `[Authorize]` (default in `CrudController`); restrict
  sensitive modules with `[Authorize(Roles = "...")]`.
- Use `[ValidateAntiForgeryToken]` on every POST.
- Prefer enum string conversions in EF (`HasConversion<string>().HasMaxLength(32)`).
- New enums: add Arabic translations to `EntityDisplayHelper.TranslateEnum`
  and badge styles to `EntityDisplayHelper.StatusClass`.
- Validate at boundaries; trust internal services.

## Don'ts

- Don't copy UI/text/branding from any existing accounting product.
- Don't add unrelated NuGet packages.
- Don't break `dotnet build` or `dotnet run`.
- Don't leave broken sidebar links.
- Don't write planning/decision/analysis markdown files unless the user
  asks — work from the conversation and from this CLAUDE.md.

## When extending the system

1. Add the entity → register in DbContext → add labels.
2. Add a `CrudController<T>` subclass.
3. Re-link the sidebar (already wired for the canonical modules).
4. If totals/posting/inventory side-effects are involved, route through a
   service rather than the controller.
5. Delete `bestgen.db` so the schema regenerates, then `dotnet run`.

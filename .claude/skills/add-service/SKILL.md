---
name: add-service
description: Add a new business-logic service in Services/ — the right place for posting, totals, stock movement, payroll, balance checks, side effects. Use when logic is more than CRUD and shouldn't sit in a controller.
---

# Add a business-logic service

CLAUDE.md is explicit: **keep controllers thin**. Anything beyond plain
CRUD belongs in `Services/`. Existing services are the reference:

- `InvoiceCalculationService` — front+back recalculation of totals/VAT
- `InventoryService` — stock movements; low-stock query
- `AccountingService` — journal entry generation, balance check, expense
  cash/bank deduction, paid-expense journal
- `SalesInvoiceService` / `PurchaseInvoiceService` — orchestrate
  calc + inventory + accounting on save
- `DashboardService`, `ReportService` — read-side aggregations

## Steps

### 1. Create the file `Services/<Name>Service.cs`

```csharp
using bestgen.Data;
using bestgen.Models;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services;

public class PayrollService
{
    private readonly ApplicationDbContext _db;
    private readonly AccountingService _accounting;

    public PayrollService(ApplicationDbContext db, AccountingService accounting)
    {
        _db = db;
        _accounting = accounting;
    }

    public async Task<PayrollEntry> ApproveAsync(int id) { ... }
}
```

### 2. Register it in `Program.cs`

Find the `builder.Services.AddScoped<...Service>()` block and add yours
alongside its peers (sales/purchase/accounting services live together):

```csharp
builder.Services.AddScoped<PayrollService>();
```

`Scoped` is correct in 99% of cases — it's per-HTTP-request.

### 3. Inject into the controller

```csharp
public class PayrollEntriesController : Controller
{
    private readonly PayrollService _payroll;
    public PayrollEntriesController(PayrollService payroll) => _payroll = payroll;
}
```

### 4. Conventions for the service body

- All EF queries are async (`ToListAsync`, `FirstOrDefaultAsync`,
  `SaveChangesAsync`).
- Use `decimal` for money (precision is configured at 18,2 globally).
- For posting flows, **always** route through `AccountingService` so the
  debit/credit balance check runs and `JournalEntryStatus.Posted` is set
  with the source module tagged.
- For stock changes, **always** route through `InventoryService` so a
  matching `StockMovement` row is written and `Product.CurrentStock`
  stays consistent.
- Validate at boundaries (controllers/UI). Inside services, trust callers
  passed sane data — don't double-validate.
- Wrap multi-step writes in `_db.Database.BeginTransactionAsync()` if a
  failure mid-flow could leave inconsistent state (e.g. journal posted
  but stock not deducted).

## Don'ts

- Don't query the DB from the controller. Even a simple `Where` belongs in
  a service if any business rule (filters, role-aware visibility, scoping)
  is involved.
- Don't write to `JournalEntries`, `StockMovements`, or any balance column
  directly — call the service that owns them.
- Don't hide service registration in a partial class or extension method
  unless three+ peers do the same. Inline registration in `Program.cs` is
  the project norm.

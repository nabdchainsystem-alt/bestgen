---
name: add-report-card
description: Add a new report card to the Reports Center (the /Reports page) and wire it to a query under Services/Reports/. Use when the user asks to add a new report, "add a customer aging report", "add a stock valuation report", or any new tile in قائمة التقارير.
---

# Add a report card

The Reports Center is driven by `Services/ReportService.cs`. Cards are grouped
by stable Arabic group name (محاسبة · المبيعات · المشتريات · المنتجات
والمخزون · الموارد البشرية), and each card has a stable string key (e.g.
`general-ledger`, `customer-statement`).

## Steps

### 1. Pick a stable key

Lower-kebab-case, English, will appear in the URL: `Reports/Details?id=<key>`.
Examples already in use: `general-ledger`, `customer-statement`,
`receivables-aging`. Don't reuse an existing key.

### 2. Add the card to the `Cards` list in `ReportService.cs`

Append a `new ReportCardViewModel(...)` row in the appropriate Arabic group
section (preserve grouping):

```csharp
new("تقييم المخزون", "قيمة المخزون الحالي بسعر التكلفة.", "المنتجات والمخزون", "bi-boxes", "stock-valuation"),
```

Constructor order: title (Arabic), description (Arabic), group (Arabic),
Bootstrap icon class, key.

### 3. Wire the key to a query class

a) Add the implementation to the appropriate file under `Services/Reports/`
   (e.g. `InventoryReports.cs`, `LedgerReports.cs`, `PartyReports.cs`,
   `TaxReports.cs`). The method returns `Task<ReportResult>` and accepts
   `ReportFilters`.

b) Add a switch arm to `ReportService.RunAsync`:
```csharp
"stock-valuation" => _inventory.StockValuationAsync(filters),
```

If you skip step (b), the report card still renders but clicking it shows the
"in progress" placeholder via the `_ => Task.FromResult(ReportResult.NotImplemented(key, filters))` fallback. That's an acceptable interim state if the data layer isn't ready — but never leave a card without a key.

### 4. Verify

- `/Reports` shows the new card under the right Arabic group.
- `/Reports/Details?id=stock-valuation` either runs the report (if you wired
  step 3b) or shows the placeholder.

## Don'ts

- Don't change an existing key — URLs and bookmarks depend on them.
- Don't put query logic inline in `RunAsync` — keep it in the relevant
  `*Reports.cs` class.
- Don't add a new group without a deliberate decision — the five groups are
  the canonical taxonomy.

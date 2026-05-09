---
name: add-dashboard-kpi
description: Add a KPI tile, chart series, or recent-activity list to the Bestgen dashboard. Touches DashboardService aggregation + DashboardViewModel + Views/Dashboard/Index.cshtml render + (sometimes) Chart.js config. Use when the user asks to add a metric, tile, chart, or "show X on the dashboard".
---

# Add a dashboard KPI / chart / list

The dashboard is one page (`Views/Dashboard/Index.cshtml`) backed by one
service (`Services/DashboardService.cs`) and one view model
(`DashboardViewModel` in `ViewModels/UiViewModels.cs`). All three must
move together.

## The recipe

### 1. Add the field to `DashboardViewModel`

```csharp
public decimal VatCollectedThisMonth { get; set; }
public string VatCollectedThisMonthDisplay { get; set; } = string.Empty;
```

Conventions:
- Money fields: pair a raw `decimal` with a `*Display` string for the
  pre-formatted label. The view uses the display string; reports/exports
  use the raw value.
- Counts: `int` (e.g. `LowStockCount`).
- Lists: `List<TEntity>` already projected — `RecentSalesInvoices`
  pattern.
- Charts: `List<ChartSeriesViewModel>` (or similar lightweight DTO) —
  one item per series.

### 2. Aggregate it in `DashboardService.GetDashboardAsync`

```csharp
var startOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
var vatThisMonth = sales
    .Where(invoice => invoice.InvoiceDate >= startOfMonth)
    .Sum(invoice => invoice.VatTotal);
```

Then assign on the ViewModel:
```csharp
VatCollectedThisMonth = vatThisMonth,
VatCollectedThisMonthDisplay = FormatMoney(vatThisMonth),
```

### 3. Watch out for SQLite decimal limits

Comments already in the file flag this:

> *"SQLite cannot ORDER BY decimal — materialize then sort client-side."*
> *"SQLite cannot SUM decimals — materialize then sum client-side."*

Pattern to follow:
```csharp
var values = await _context.Whatever.AsNoTracking()
    .Select(x => x.SomeDecimal).ToListAsync();
var sum = values.Sum();
```

Don't try to push the aggregation server-side for decimals — it'll crash
at runtime. Filtering server-side (`.Where(...)`) is fine; aggregating
isn't.

### 4. Render it in `Views/Dashboard/Index.cshtml`

For a KPI tile, compose `_DashboardCard`:
```razor
@await Html.PartialAsync("_DashboardCard", new DashboardCardViewModel
{
    Title = T("VAT This Month", "ضريبة القيمة المضافة هذا الشهر"),
    Value = Model.VatCollectedThisMonthDisplay,
    Icon = "bi-percent",
    Tone = "blue"
})
```

Tones to use (matching the design system):
- `emerald` — positive/healthy (sales, profit)
- `blue` — neutral information (counts, totals)
- `warning` — attention (overdue, low stock)
- `danger` — alert (negative figures, alarms)
- `navy` — headline (single most important number)

### 5. For a chart series, also touch the Chart.js block

Charts use Chart.js 4 (already loaded by `_Layout.cshtml`). Look at how
the existing sales/purchases time series is built in `Index.cshtml` —
mirror its `<canvas>` + inline `<script>` shape, and add a new
`new Chart(...)` invocation. Keep the color palette aligned with
`site.css` `:root` variables (`--emerald`, `--blue`, `--navy`).

### 6. For a recent-activity list, compose existing partials

`RecentSalesInvoices` and `UnpaidInvoices` show the pattern: project a
`List<T>` on the VM, then render a small table inside `.dashboard-grid`.
Use `_StatusBadge` for status pills and `decimal` formatted via the
display field.

## Verification

- `/` (dashboard) renders without exception.
- The new tile shows a sane value (not zero unless that's correct).
- Bilingual: switch language; the title and number formatting follow.
- The number is consistent with the matching report (e.g. VAT-this-month
  on dashboard == VAT Return for this month in `/Reports`).

## Don'ts

- Don't aggregate decimals server-side in EF/SQLite — materialize first.
- Don't compute anything in the Razor view. The view renders; the
  service computes. Mixing makes refactors painful.
- Don't add a tile without a real source for the number. If the number
  isn't yet computable, hold off — placeholder zeros mislead users.
- Don't introduce a new chart library. Chart.js 4 is the standard.
- Don't break the responsive grid: a new tile must fit `metrics-grid` /
  `dashboard-grid` shells; don't open custom layout shapes per-tile.

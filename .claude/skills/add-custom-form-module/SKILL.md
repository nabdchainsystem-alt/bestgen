---
name: add-custom-form-module
description: Add a Bestgen module with a multi-line form like SalesInvoices, PurchaseInvoices, or JournalEntries — header row with multiple line items, totals/VAT recalculation, and side effects on stock or accounting. Use when the user asks for a "new invoice type", "document with line items", "voucher with multiple lines", or anything that mirrors the SalesInvoices flow.
---

# Add a custom (hand-rolled) form module to Bestgen

Use this recipe when the entity has **child line items** and **side effects**
(inventory movement, journal entries, balance updates). For simple flat tables,
use `add-crud-module` instead.

The canonical references in the codebase:
- `Controllers/SalesInvoicesController.cs` (+ `Views/SalesInvoices/`)
- `Controllers/PurchaseInvoicesController.cs` (+ `Views/PurchaseInvoices/`)
- `Controllers/JournalEntriesController.cs` (+ `Views/JournalEntries/`)
- Services: `SalesInvoiceService`, `PurchaseInvoiceService`,
  `InvoiceCalculationService`, `InventoryService`, `AccountingService`.

## Steps

### 1. Two entities (header + line)

In `Models/Entities.cs`:
```csharp
public class GoodsReceipt
{
    public int Id { get; set; }
    [Required, StringLength(32)] public string GoodsReceiptNumber { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public GoodsReceiptStatus Status { get; set; } = GoodsReceiptStatus.Draft;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<GoodsReceiptItem> Items { get; set; } = new List<GoodsReceiptItem>();
}

public class GoodsReceiptItem
{
    public int Id { get; set; }
    public int GoodsReceiptId { get; set; }
    public GoodsReceipt? GoodsReceipt { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal { get; set; }
}
```

### 2. Register both DbSets in `Data/ApplicationDbContext.cs`

Group them together:
```csharp
public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();
public DbSet<GoodsReceiptItem> GoodsReceiptItems => Set<GoodsReceiptItem>();
```

### 3. ViewModel for the form in `ViewModels/`

The header view-model bundles header fields + a list of editable line items.
Mirror `SalesInvoiceFormViewModel`:
```csharp
public class GoodsReceiptFormViewModel
{
    public int? Id { get; set; }
    public string GoodsReceiptNumber { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.Today;
    public int SupplierId { get; set; }
    public int WarehouseId { get; set; }
    public string? Notes { get; set; }
    public List<GoodsReceiptLineViewModel> Items { get; set; } = new();
}
```

### 4. Service in `Services/` — never put logic in the controller

```csharp
public class GoodsReceiptService
{
    private readonly ApplicationDbContext _db;
    private readonly InventoryService _inventory;
    private readonly AccountingService _accounting;

    public async Task<GoodsReceipt> CreateAsync(GoodsReceiptFormViewModel vm) { ... }
    public async Task<GoodsReceipt> UpdateAsync(GoodsReceiptFormViewModel vm) { ... }
}
```

Register it in `Program.cs`:
```csharp
builder.Services.AddScoped<GoodsReceiptService>();
```

Side effects to delegate:
- Inventory in/out → `InventoryService` (creates `StockMovement` rows, updates
  `Product.CurrentStock`).
- Posting → `AccountingService.GenerateJournalEntry...` (debits/credits, balance
  check, `JournalEntryStatus.Posted`).
- Cash/bank deduction → `AccountingService.DeductFromPaidSource`.

### 5. Custom controller (mirror `SalesInvoicesController`)

Actions: `Index`, `Details(id)`, `Create [GET/POST]`, `Edit(id) [GET/POST]`,
`Delete(id) [POST]`. Decorate POSTs with `[ValidateAntiForgeryToken]`.

`Create [GET]` and `Edit [GET]` populate dropdowns (suppliers, warehouses,
products) into ViewBag. `POST` actions delegate to the service and redirect
on success.

### 6. Custom views in `Views/<Module>/`

Five files: `Index.cshtml`, `Create.cshtml`, `Edit.cshtml`, `Details.cshtml`,
`Delete.cshtml`, plus a `_Form.cshtml` partial shared by Create/Edit.
Compose with the shared partials: `_PageHeader`, `_SearchBar`,
`_StatusBadge`, `_ActionButtons`, `_EmptyState`. Stay inside `data-panel` /
`form-panel` / `details-panel` shells so visual identity is consistent.

### 7. Labels + list config in `EntityDisplayHelper.cs`

Even with custom views, register the header in `ModuleNames`/`ModuleNamesEn`
(for `_PageHeader`) and `ListProperties` (only used if you ever fall back to
a generic list).

### 8. Sidebar link (see `add-sidebar-item`) and DB reset (see `reset-db`)

## Don'ts

- Don't put recalculation, VAT, posting, or stock changes in the controller —
  they belong in services.
- Don't bypass `InventoryService` to write stock changes directly.
- Don't post journals manually — call `AccountingService` so balance is
  validated and the source module is tagged.
- Don't forget to delete child line rows when editing (re-create from the VM).

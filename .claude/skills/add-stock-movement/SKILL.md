---
name: add-stock-movement
description: Record a stock change (in, out, transfer, adjustment, opening, return) through StockMovementService so Product.CurrentStock and the audit trail stay consistent. Use when a new transaction type affects inventory — returns, manual adjustments, scrap, asset deployment, etc.
---

# Record a stock movement through `StockMovementService`

`Product.CurrentStock` is the live balance. Every change to it must come
with a matching `StockMovement` row for audit. The only place that writes
both is `StockMovementService` — go through it.

`InventoryService` is a thin shim exposing invoice-shaped helpers
(`ReduceStockAsync(SalesInvoice)`, `IncreaseStockAsync(PurchaseInvoice)`).
For new transaction types, use `StockMovementService` directly.

## The API

```csharp
public Task<StockMovement?> RecordAsync(
    int productId,
    int warehouseId,
    StockMovementType type,
    decimal quantity,        // signed: positive = in, negative = out
    decimal unitCost,
    string? reference,
    DateTime? movementDate)
```

Plus higher-level helpers already in the service:
`RecordSalesIssueAsync`, `RecordPurchaseReceiptAsync`,
`RecordTransferAsync`, `RecordCountAdjustmentAsync`, `RecordOpeningAsync`,
`RecordReturnInboundAsync`, `RecordReturnOutboundAsync`.

## When to add a new helper vs call `RecordAsync` directly

- **Add a new helper** if the transaction shape recurs (e.g.
  `RecordScrapAsync(ScrapEntry scrap)`). Group the per-line iteration and
  reference formatting there so callers stay clean.
- **Call `RecordAsync` directly** for one-off ad-hoc movements.

## The recipe — adding a new helper

### 1. Pick the right `StockMovementType`

Existing values: `Sales`, `Purchase`, `Transfer`, `Adjustment`, `Opening`,
`Return`. If your new flow doesn't fit:
- Reuse `Adjustment` for one-off corrections.
- Add a new enum member only if it's a **distinct business event** that
  reports need to filter on. New enum value also requires Arabic
  translation + badge color (see `add-enum`) + DB reset.

### 2. Add the helper on `StockMovementService`

```csharp
public async Task RecordScrapAsync(ScrapEntry scrap, IEnumerable<ScrapEntryItem> items)
{
    foreach (var item in items)
    {
        await RecordAsync(
            productId: item.ProductId,
            warehouseId: scrap.WarehouseId,
            type: StockMovementType.Adjustment,
            quantity: -item.Quantity,                   // out
            unitCost: item.UnitCost,
            reference: $"Scrap {scrap.ScrapNumber}",
            movementDate: scrap.Date);
    }
}
```

### 3. Sign the quantity correctly

- Stock **in** (purchase, return inbound, opening, transfer-in): positive.
- Stock **out** (sales, return outbound, scrap, transfer-out): negative.

`RecordAsync` updates `Product.CurrentStock += quantity` and stamps the
movement row, so the sign IS the direction.

### 4. Wire it from the orchestrating service

```csharp
// in ScrapService.ConfirmAsync
await _movements.RecordScrapAsync(scrap, scrap.Items);
await _db.SaveChangesAsync();
```

The service `Add`s movement rows and updates products; **the caller is
responsible for `SaveChangesAsync`**, ideally in the same transaction
that confirms the source document.

### 5. Pair with a journal posting if money moved

A scrap reduces inventory value. If you also need to debit
`AccountCodes.Scrap` and credit `AccountCodes.Inventory`, follow the
`post-journal-entry` skill in the same service flow.

## Verification

- `Product.CurrentStock` changes by the expected amount.
- A row exists in `StockMovements` with the right `Type`, `Quantity`
  (signed), `Reference`, and `MovementDate`.
- `/Reports → Inventory Ledger` shows the row.
- `GetLowStockProductsAsync` re-flags items that crossed the threshold.

## Don'ts

- Don't write `Product.CurrentStock = ...` anywhere outside
  `StockMovementService` — the audit row will be missing.
- Don't insert into `StockMovements` directly without updating
  `CurrentStock` — they'll drift.
- Don't ignore `TrackInventory` — products with `TrackInventory = false`
  shouldn't get movement rows. Filter at the helper layer.
- Don't pass an unsigned quantity and a separate "direction" flag. The
  sign is the direction; mixed conventions break reports.
- Don't `SaveChangesAsync` inside the helper — leave it to the caller so
  related writes (journal entry, source-document status flip) commit
  atomically.

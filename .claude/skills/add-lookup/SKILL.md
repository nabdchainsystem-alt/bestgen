---
name: add-lookup
description: Wire an FK property (anything ending in Id, e.g. TaxRateId, BranchId, JobTitleId) so its dropdown auto-populates on Create/Edit forms in the generic CRUD framework. Use when an entity references a new master table and the form shows an empty select.
---

# Add a foreign-key lookup

The generic CRUD framework auto-populates dropdowns for any property whose
name ends in `Id` (and isn't `Id` itself), via
`CrudController.PopulateLookupsAsync`. The dispatch is hardcoded — if your
new FK isn't listed there, the dropdown silently renders empty.

This is the **easy-to-miss step** right after `add-crud-module` whenever the
new entity will be referenced from other entities.

## Symptom

You added `public int TaxRateId { get; set; }` to `SalesInvoice`, opened
`/SalesInvoices/Create`, and the Tax Rate dropdown is empty even though
`/TaxRates` has rows.

## Fix

Add a branch to the `switch` in
`Controllers/CrudController.cs → PopulateLookupsAsync`. Place it next to
similar lookups (alphabetical-ish by the entity, or grouped by domain).

```csharp
"TaxRateId" => await Context.TaxRates.AsNoTracking()
    .Where(x => x.IsActive)
    .OrderBy(x => x.Code)
    .Select(x => new SelectListItem
    {
        Text = $"{x.NameAr} ({x.Rate}%)",
        Value = x.Id.ToString()
    }).ToListAsync(),
```

### Conventions

- Use `AsNoTracking()` — these are read-only lists.
- Filter by `IsActive` if the entity has it; users shouldn't pick retired
  rows for new transactions.
- Order by the most human-readable column (Arabic name, code).
- For composite labels, format like `"{Code} - {NameAr}"` —
  the `Account` and `FixedAsset` branches do this. Helpful when the
  internal code matters to the user.
- The `Text` should be Arabic when the user is in Arabic UI. Hardcoding
  `NameAr` is fine here — the Reports/Dashboard code uses `PickName`
  helpers but the lookup dispatch keeps it simple.

### When the property name needs to map to a different table

Match by name patterns the same way existing branches do (e.g.
`"FromWarehouseId" or "ToWarehouseId" => Warehouses`). Use the `or`
pattern matching syntax to coalesce.

## Hand-rolled controllers (SalesInvoices, PurchaseInvoices, etc.)

Custom controllers don't go through `PopulateLookupsAsync`. Each one
populates ViewBag dropdowns manually in its `Create [GET]` / `Edit [GET]`
actions. Add the new lookup query alongside the existing ones in that
controller.

## Verification

- Open the form. The new dropdown shows the rows.
- Default (first) option is the most reasonable starting point — IsActive
  rows only.
- Save the form. The selected Id round-trips to the entity.

## Don'ts

- Don't sort `decimal` columns at the SQL level — SQLite can't ORDER BY
  decimal. Materialize, then sort client-side (see how `InventoryService`
  handles low-stock).
- Don't load full nested objects in lookups. `Select` only what the label
  needs — these calls run on every form render.
- Don't `Include()` related data in lookup queries; it inflates the
  payload for no UI benefit.

---
name: add-numbering-policy
description: Wire a new transactional document type into the NumberingPolicies system so it gets sequential numbering like INV-2026-00042. Use when adding a new document module (refund, voucher, contract, etc.) that needs a per-document-type counter and configurable Arabic prefix.
---

# Add a numbering policy for a new document type

`DocumentNumberingService.NextAsync(documentType, fallbackPrefix)` returns
the next number for a given document type, formatted by the configured
`NumberingPolicy` row. Format tokens: `{prefix}`, `{yyyy}`, `{MM}`,
`{0000}` (zero-padded counter).

## When you need this

You're adding a new transactional document module (something with a
visible "document number" on its UI — invoices, vouchers, receipts,
contracts). Master data (customers, products) doesn't need this; their
codes are user-entered.

## The recipe

### 1. Pick the `documentType` key

Use the **entity class name** (PascalCase), e.g. `"SupplierPayment"`,
`"GoodsReceipt"`. This matches the convention seeded in `DbSeeder.cs`.
The key is what your service passes to `NextAsync`.

### 2. Seed a row in `SeedData/DbSeeder.cs`

Find the `new NumberingPolicy { ... }` block near line 80 and add a row
matching the existing format:

```csharp
new NumberingPolicy
{
    DocumentType = "SupplierPayment",
    DisplayNameAr = "دفعة مورد",
    DisplayNameEn = "Supplier Payment",
    Prefix = "PAY",
    Format = "{prefix}-{yyyy}-{00000}",
    ResetAnnually = true
},
```

Conventions:
- `Prefix` is short (≤4 chars), uppercase Latin. Pick something
  unambiguous (`PAY`, not `P`).
- `Format` default is `{prefix}-{yyyy}-{00000}` (5-digit counter, annual
  reset). For monthly-resetting payroll-style numbers use
  `{prefix}-{yyyy}{MM}-{00000}` and `ResetAnnually = false`.
- `ResetAnnually = true` resets the counter to 1 every January 1.
- Counter width is taken from the `{0000}` token literally — 4 zeros =
  4-digit, 5 zeros = 5-digit. Match siblings for visual consistency.

### 3. Reset the DB so the seed runs

```bash
rm bestgen.db
dotnet run
```

`DbSeeder` only seeds NumberingPolicies when they don't already exist
(check the seeder logic if unsure). On a fresh DB, your row appears in
`/NumberingPolicies`.

### 4. Use it in your service

In the service that creates the document:

```csharp
public class SupplierPaymentService
{
    private readonly DocumentNumberingService _numbering;
    // ... ctor ...

    public async Task<SupplierPayment> CreateAsync(SupplierPaymentFormViewModel vm)
    {
        var payment = new SupplierPayment
        {
            PaymentNumber = await _numbering.NextAsync("SupplierPayment", "PAY"),
            // ... rest of fields ...
        };
        _db.SupplierPayments.Add(payment);
        await _db.SaveChangesAsync();
        return payment;
    }
}
```

The `fallbackPrefix` argument is used only if the policy row is missing
(e.g. fresh DB before the seeder ran, or a misconfigured environment).
It produces something like `PAY-2026-00001` so the entity is still
saveable.

### 5. Preview without consuming

For UI that shows "next number will be..." use `PeekAsync` instead of
`NextAsync` — it doesn't increment the counter:

```csharp
ViewBag.NextPaymentNumber = await _numbering.PeekAsync("SupplierPayment", "PAY");
```

## Verification

- `/NumberingPolicies` lists the new row with the right prefix and format.
- Creating the first document of this type produces e.g. `PAY-2026-00001`.
- The next one is `PAY-2026-00002`. The counter persists across restarts.
- After Jan 1, the counter resets to `00001` if `ResetAnnually = true`.
- The user can edit the policy at runtime (prefix, format, reset rule)
  and the next number reflects the change.

## Don'ts

- Don't generate document numbers inline in services with
  `Guid.NewGuid()` or string concatenation — bypasses the policy and
  user customization.
- Don't share a `documentType` key across two unrelated documents — the
  counter would interleave incorrectly.
- Don't forget the seed row. Without it, every document falls back to
  `PREFIX-YYYY-00001` forever (the fallback never increments anything).
- Don't expose the counter as user-editable in the UI — let users edit
  prefix/format/reset rules, but `CurrentSequence` is system-managed.
- Don't rename a `documentType` key after data exists. Old documents
  reference it by string, and the counter resets to zero behind a fresh
  key.

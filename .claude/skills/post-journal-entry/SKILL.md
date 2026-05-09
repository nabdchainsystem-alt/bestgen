---
name: post-journal-entry
description: Create and post a journal entry to the General Ledger correctly via AccountingService.BuildAndAddEntryAsync — balanced debits/credits, source-module tagging, posting status, and entry numbering. Use whenever a new transaction type needs to hit the GL (payments, refunds, adjustments, payroll runs, asset disposals, manual postings).
---

# Post a journal entry through `AccountingService`

All GL postings in Bestgen flow through `AccountingService` so balance is
validated, the entry is numbered, and the source module is tagged for
traceability in `JournalEntries.SourceModule`.

Reference implementations:
- `AccountingService.CreateSalesJournalEntryAsync` — Sales
- `AccountingService.CreatePurchaseJournalEntryAsync` — Purchases
- `AccountingService.ApplyPaidExpenseAsync` — Cash/bank deduction

## The recipe

### 1. Add the method on `AccountingService`

Don't post journals from a controller or from another service directly —
add a method to `AccountingService` so the posting recipe stays in one
place.

```csharp
public async Task CreateSupplierPaymentJournalEntryAsync(SupplierPayment payment)
{
    if (payment.Status is ReceiptStatus.Draft or ReceiptStatus.Cancelled)
    {
        return;  // Nothing to post for unconfirmed/cancelled.
    }

    var ap = await _chart.ResolveAsync(AccountCodes.AccountsPayable);
    var cash = await _chart.ResolveAsync(
        payment.PaymentMethod == PaymentMethod.Bank ? AccountCodes.Bank : AccountCodes.Cash);

    await BuildAndAddEntryAsync(
        entryDate: payment.Date,
        sourceModule: nameof(SupplierPayment),
        description: $"Supplier payment {payment.PaymentNumber}",
        lines: new[]
        {
            new JournalEntryLine { AccountId = ap.Id,   Debit = payment.Amount, Credit = 0,              Description = "Settle payable" },
            new JournalEntryLine { AccountId = cash.Id, Debit = 0,              Credit = payment.Amount, Description = "Cash/Bank out" }
        },
        status: JournalEntryStatus.Posted);
}
```

### 2. Use `nameof(...)` for `sourceModule`

The convention in CLAUDE.md and existing services: `sourceModule` is the
**entity class name** (e.g. `nameof(SalesInvoice)`, not `"sales"`). This
is the link reports use to filter "all entries from sales."

### 3. Resolve accounts via `ChartOfAccounts`

Don't hardcode account ids. Use `_chart.ResolveAsync(AccountCodes.X)` —
constants live in `AccountCodes`. If you need a new account code, add it
there and to the seeded chart in `DbSeeder`.

### 4. Build balanced lines

Total debits must equal total credits. `BuildAndAddEntryAsync` throws
`InvalidOperationException` if not balanced — that's the safety net, but
your method should also be obviously balanced by construction.

For partial payments / refunds / split allocations, double-check the math
before passing the lines in.

### 5. Choose `status` deliberately

- `JournalEntryStatus.Draft` — for entries the user reviews before
  posting (e.g. user-created journals via JournalEntries form).
- `JournalEntryStatus.Posted` — for system-generated entries from a
  confirmed transaction (sales invoice issued, payment confirmed).

The `BuildAndAddEntryAsync` default is `Draft`. Override when the caller
already represents a confirmed action.

### 6. Wire it in from the orchestrating service

Call your new posting method from the relevant service flow:

```csharp
// in SupplierPaymentService.ConfirmAsync
await _accounting.CreateSupplierPaymentJournalEntryAsync(payment);
await _db.SaveChangesAsync();
```

`AccountingService` only `Add`s the entity; **the caller is responsible
for `SaveChangesAsync`**. Bundle it with the other writes in the same
transaction so a failure doesn't leave a half-posted state.

### 7. Reverse on cancel

When the source transaction is cancelled or deleted, post a **reversing**
journal entry — don't delete the original. Bestgen's GL is append-only.
Use the same lines with `Debit` and `Credit` swapped, description
prefixed `"Reversal of …"`, and `sourceModule` unchanged so the reversal
threads to the same source.

## Verification

After triggering the flow:
- A new row appears in `/JournalEntries` with status `مرحل` (Posted).
- `EntryNumber` follows the policy (e.g. `JE-2026-00042`).
- Lines balance (TotalDebit == TotalCredit, both > 0).
- `SourceModule` shows the originating entity name.
- Affected account balances change (visible in `/Reports → Trial Balance`).

## Don'ts

- Don't write to `JournalEntries` from any service except `AccountingService`.
- Don't bypass `BuildAndAddEntryAsync` to skip the balance check. If your
  posting "doesn't balance," your debit/credit logic is wrong.
- Don't reuse a previously generated `EntryNumber` — let
  `GenerateEntryNumberAsync` produce a fresh one for each entry.
- Don't post in `Draft` status from a confirmed transaction; that hides
  the real impact from reports.
- Don't delete or edit posted entries to "fix" them — post a reversal.

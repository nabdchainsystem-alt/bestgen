using bestgen.Data;
using bestgen.Models;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services;

/// <summary>
/// Posts a debit note issued to a supplier (typically for returned goods or
/// a billing adjustment that reduces what we owe them).
///   DR Accounts Payable (2000) - reduces what we owe.
///   CR Purchase Returns (5900) - reverses the original purchase.
///   CR VAT Input (2110) - reverses the input vat we previously recovered.
/// Supplier balance is recalculated post-commit.
/// </summary>
public class DebitNoteService
{
    private readonly ApplicationDbContext _context;
    private readonly AccountingService _accounting;
    private readonly ChartOfAccounts _chart;
    private readonly PartyBalanceService _partyBalance;

    public DebitNoteService(
        ApplicationDbContext context,
        AccountingService accounting,
        ChartOfAccounts chart,
        PartyBalanceService partyBalance)
    {
        _context = context;
        _accounting = accounting;
        _chart = chart;
        _partyBalance = partyBalance;
    }

    public async Task PrepareAsync(DebitNote note)
    {
        if (string.IsNullOrWhiteSpace(note.DebitNoteNumber))
        {
            note.DebitNoteNumber = await GenerateNumberAsync();
        }
    }

    public async Task PostAsync(DebitNote note)
    {
        if (note.Status != CreditNoteStatus.Issued)
        {
            return;
        }

        var total = note.Amount + note.VatAmount;
        if (total <= 0)
        {
            return;
        }

        var ap = await _chart.ResolveAsync(AccountCodes.AccountsPayable);
        var purchaseReturns = await _chart.ResolveAsync(AccountCodes.PurchaseReturns);
        var vatIn = await _chart.ResolveAsync(AccountCodes.VatInput);

        var lines = new List<JournalEntryLine>
        {
            new() { AccountId = ap.Id, Debit = total, Credit = 0, Description = "Accounts payable" },
            new() { AccountId = purchaseReturns.Id, Debit = 0, Credit = note.Amount, Description = "Purchase returns" }
        };
        if (note.VatAmount > 0)
        {
            lines.Add(new() { AccountId = vatIn.Id, Debit = 0, Credit = note.VatAmount, Description = "Input VAT reversal" });
        }

        await _accounting.BuildAndAddEntryAsync(
            entryDate: note.Date,
            sourceModule: nameof(DebitNote),
            description: $"Debit note {note.DebitNoteNumber}",
            lines: lines);
    }

    public async Task ApplyAsync(DebitNote note)
    {
        await _partyBalance.RecalculateSupplierAsync(note.SupplierId);
    }

    private async Task<string> GenerateNumberAsync()
    {
        var next = await _context.DebitNotes.CountAsync() + 1;
        return $"DBN-{DateTime.Today:yyyy}-{next:00000}";
    }
}

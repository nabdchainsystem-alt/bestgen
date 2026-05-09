using bestgen.Data;
using bestgen.Models;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services;

/// <summary>
/// Posts a credit note (issued back to a customer, typically for returned goods or
/// a billing adjustment).
///   DR Sales Returns (4900) - net amount.
///   DR VAT Output (2100)    - vat portion that we are reversing.
///   CR Accounts Receivable  - reduces what the customer owes us.
/// Customer balance is recalculated post-commit.
/// </summary>
public class CreditNoteService
{
    private readonly ApplicationDbContext _context;
    private readonly AccountingService _accounting;
    private readonly ChartOfAccounts _chart;
    private readonly PartyBalanceService _partyBalance;

    public CreditNoteService(
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

    public async Task PrepareAsync(CreditNote note)
    {
        if (string.IsNullOrWhiteSpace(note.CreditNoteNumber))
        {
            note.CreditNoteNumber = await GenerateNumberAsync();
        }
    }

    public async Task PostAsync(CreditNote note)
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

        var salesReturns = await _chart.ResolveAsync(AccountCodes.SalesReturns);
        var vat = await _chart.ResolveAsync(AccountCodes.VatOutput);
        var ar = await _chart.ResolveAsync(AccountCodes.AccountsReceivable);

        var lines = new List<JournalEntryLine>
        {
            new() { AccountId = salesReturns.Id, Debit = note.Amount, Credit = 0, Description = "Sales returns" }
        };
        if (note.VatAmount > 0)
        {
            lines.Add(new() { AccountId = vat.Id, Debit = note.VatAmount, Credit = 0, Description = "Output VAT reversal" });
        }
        lines.Add(new() { AccountId = ar.Id, Debit = 0, Credit = total, Description = "Accounts receivable" });

        await _accounting.BuildAndAddEntryAsync(
            entryDate: note.Date,
            sourceModule: nameof(CreditNote),
            description: $"Credit note {note.CreditNoteNumber}",
            lines: lines);
    }

    public async Task ApplyAsync(CreditNote note)
    {
        await _partyBalance.RecalculateCustomerAsync(note.CustomerId);
    }

    private async Task<string> GenerateNumberAsync()
    {
        var next = await _context.CreditNotes.CountAsync() + 1;
        return $"CN-{DateTime.Today:yyyy}-{next:00000}";
    }
}

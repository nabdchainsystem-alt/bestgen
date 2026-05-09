using bestgen.Data;
using bestgen.Models;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services;

/// <summary>
/// Posts a free-form general receipt or payment outside the customer/supplier
/// flows.
///   ReceiptType.Receipt (cash in): DR Cash/Bank, CR <Account>.
///   ReceiptType.Payment (cash out): DR <Account>, CR Cash/Bank.
/// The cash/bank side comes from CashBoxId or BankAccountId; the corresponding
/// CashBox/BankAccount.CurrentBalance is also updated in the same commit.
/// </summary>
public class GeneralReceiptService
{
    private readonly ApplicationDbContext _context;
    private readonly AccountingService _accounting;
    private readonly ChartOfAccounts _chart;

    public GeneralReceiptService(
        ApplicationDbContext context,
        AccountingService accounting,
        ChartOfAccounts chart)
    {
        _context = context;
        _accounting = accounting;
        _chart = chart;
    }

    public async Task PrepareAsync(GeneralReceipt receipt)
    {
        if (string.IsNullOrWhiteSpace(receipt.ReceiptNumber))
        {
            receipt.ReceiptNumber = await GenerateNumberAsync();
        }
    }

    public async Task PostAsync(GeneralReceipt receipt)
    {
        if (receipt.Status != ReceiptStatus.Confirmed || receipt.Amount <= 0)
        {
            return;
        }

        var fromBank = receipt.BankAccountId.HasValue;
        var cashOrBank = await _chart.ResolveAsync(fromBank ? AccountCodes.Bank : AccountCodes.Cash);
        var counterAccountId = receipt.AccountId;

        var lines = receipt.ReceiptType == GeneralReceiptType.Receipt
            ? new[]
            {
                new JournalEntryLine { AccountId = cashOrBank.Id, Debit = receipt.Amount, Credit = 0, Description = "Cash in" },
                new JournalEntryLine { AccountId = counterAccountId, Debit = 0, Credit = receipt.Amount, Description = receipt.Description ?? "General receipt" }
            }
            : new[]
            {
                new JournalEntryLine { AccountId = counterAccountId, Debit = receipt.Amount, Credit = 0, Description = receipt.Description ?? "General payment" },
                new JournalEntryLine { AccountId = cashOrBank.Id, Debit = 0, Credit = receipt.Amount, Description = "Cash out" }
            };

        await _accounting.BuildAndAddEntryAsync(
            entryDate: receipt.Date,
            sourceModule: nameof(GeneralReceipt),
            description: $"General {receipt.ReceiptType.ToString().ToLowerInvariant()} {receipt.ReceiptNumber}",
            lines: lines);

        await UpdateCashAccountAsync(receipt, fromBank);
    }

    private async Task UpdateCashAccountAsync(GeneralReceipt receipt, bool fromBank)
    {
        var sign = receipt.ReceiptType == GeneralReceiptType.Receipt ? 1 : -1;
        var delta = sign * receipt.Amount;

        if (fromBank && receipt.BankAccountId.HasValue)
        {
            var bank = await _context.BankAccounts.FindAsync(receipt.BankAccountId.Value);
            if (bank is not null)
            {
                bank.CurrentBalance += delta;
                bank.UpdatedAt = DateTime.UtcNow;
            }
        }
        else if (receipt.CashBoxId.HasValue)
        {
            var cashBox = await _context.CashBoxes.FindAsync(receipt.CashBoxId.Value);
            if (cashBox is not null)
            {
                cashBox.CurrentBalance += delta;
                cashBox.UpdatedAt = DateTime.UtcNow;
            }
        }
    }

    private async Task<string> GenerateNumberAsync()
    {
        var next = await _context.GeneralReceipts.CountAsync() + 1;
        return $"GR-{DateTime.Today:yyyy}-{next:00000}";
    }
}

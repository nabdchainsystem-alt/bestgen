using bestgen.Data;
using bestgen.Models;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services;

/// <summary>
/// Posts a sales receipt to the ledger:
///  - DR Cash (1000) or Bank (1010), depending on the payment method.
///  - CR Accounts Receivable (1100).
///
/// Receipts are "on-account" payments tracked against the customer; an
/// optional invoice reference is captured on the receipt's Reference field
/// for traceability. Customer balance is recomputed post-commit.
/// </summary>
public class SalesReceiptService
{
    private readonly ApplicationDbContext _context;
    private readonly AccountingService _accounting;
    private readonly ChartOfAccounts _chart;
    private readonly PartyBalanceService _partyBalance;

    public SalesReceiptService(
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

    public async Task PrepareAsync(SalesReceipt receipt)
    {
        if (string.IsNullOrWhiteSpace(receipt.ReceiptNumber))
        {
            receipt.ReceiptNumber = await GenerateReceiptNumberAsync();
        }
    }

    public async Task PostAsync(SalesReceipt receipt)
    {
        if (receipt.Status != ReceiptStatus.Confirmed || receipt.Amount <= 0)
        {
            return;
        }

        var debitCode = receipt.PaymentMethod == PaymentMethod.Bank
            ? AccountCodes.Bank
            : AccountCodes.Cash;

        var debit = await _chart.ResolveAsync(debitCode);
        var ar = await _chart.ResolveAsync(AccountCodes.AccountsReceivable);

        await _accounting.BuildAndAddEntryAsync(
            entryDate: receipt.Date,
            sourceModule: nameof(SalesReceipt),
            description: $"Sales receipt {receipt.ReceiptNumber}",
            lines: new[]
            {
                new JournalEntryLine { AccountId = debit.Id, Debit = receipt.Amount, Credit = 0, Description = "Cash collection" },
                new JournalEntryLine { AccountId = ar.Id, Debit = 0, Credit = receipt.Amount, Description = "Accounts receivable" }
            });
    }

    public async Task ApplyAsync(SalesReceipt receipt)
    {
        await _partyBalance.RecalculateCustomerAsync(receipt.CustomerId);
        await UpdateCashAccountAsync(receipt);
    }

    private async Task UpdateCashAccountAsync(SalesReceipt receipt)
    {
        if (receipt.Status != ReceiptStatus.Confirmed)
        {
            return;
        }

        if (receipt.PaymentMethod == PaymentMethod.Bank && receipt.BankAccountId.HasValue)
        {
            var bank = await _context.BankAccounts.FindAsync(receipt.BankAccountId.Value);
            if (bank is not null)
            {
                bank.CurrentBalance += receipt.Amount;
                bank.UpdatedAt = DateTime.UtcNow;
            }
        }
        else if (receipt.CashBoxId.HasValue)
        {
            var cashBox = await _context.CashBoxes.FindAsync(receipt.CashBoxId.Value);
            if (cashBox is not null)
            {
                cashBox.CurrentBalance += receipt.Amount;
                cashBox.UpdatedAt = DateTime.UtcNow;
            }
        }
    }

    private async Task<string> GenerateReceiptNumberAsync()
    {
        var next = await _context.SalesReceipts.CountAsync() + 1;
        return $"SR-{DateTime.Today:yyyy}-{next:00000}";
    }
}

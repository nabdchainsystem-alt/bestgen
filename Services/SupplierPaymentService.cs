using bestgen.Data;
using bestgen.Models;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services;

/// <summary>
/// Posts a supplier payment to the ledger:
///   DR Accounts Payable (2000) - reduces what we owe.
///   CR Cash (1000) or Bank (1010), depending on PaymentMethod.
/// Cash/bank balance is decremented in the same commit; supplier balance is
/// recomputed post-commit.
/// </summary>
public class SupplierPaymentService
{
    private readonly ApplicationDbContext _context;
    private readonly AccountingService _accounting;
    private readonly ChartOfAccounts _chart;
    private readonly PartyBalanceService _partyBalance;

    public SupplierPaymentService(
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

    public async Task PrepareAsync(SupplierPayment payment)
    {
        if (string.IsNullOrWhiteSpace(payment.PaymentNumber))
        {
            payment.PaymentNumber = await GenerateNumberAsync();
        }
    }

    public async Task PostAsync(SupplierPayment payment)
    {
        if (payment.Status != ReceiptStatus.Confirmed || payment.Amount <= 0)
        {
            return;
        }

        var creditCode = payment.PaymentMethod == PaymentMethod.Bank
            ? AccountCodes.Bank
            : AccountCodes.Cash;

        var ap = await _chart.ResolveAsync(AccountCodes.AccountsPayable);
        var cashOrBank = await _chart.ResolveAsync(creditCode);

        await _accounting.BuildAndAddEntryAsync(
            entryDate: payment.Date,
            sourceModule: nameof(SupplierPayment),
            description: $"Supplier payment {payment.PaymentNumber}",
            lines: new[]
            {
                new JournalEntryLine { AccountId = ap.Id, Debit = payment.Amount, Credit = 0, Description = "Accounts payable" },
                new JournalEntryLine { AccountId = cashOrBank.Id, Debit = 0, Credit = payment.Amount, Description = "Cash payment" }
            });

        await UpdateCashAccountAsync(payment);
    }

    public async Task ApplyAsync(SupplierPayment payment)
    {
        await _partyBalance.RecalculateSupplierAsync(payment.SupplierId);
    }

    private async Task UpdateCashAccountAsync(SupplierPayment payment)
    {
        if (payment.PaymentMethod == PaymentMethod.Bank && payment.BankAccountId.HasValue)
        {
            var bank = await _context.BankAccounts.FindAsync(payment.BankAccountId.Value);
            if (bank is not null)
            {
                bank.CurrentBalance -= payment.Amount;
                bank.UpdatedAt = DateTime.UtcNow;
            }
        }
        else if (payment.CashBoxId.HasValue)
        {
            var cashBox = await _context.CashBoxes.FindAsync(payment.CashBoxId.Value);
            if (cashBox is not null)
            {
                cashBox.CurrentBalance -= payment.Amount;
                cashBox.UpdatedAt = DateTime.UtcNow;
            }
        }
    }

    private async Task<string> GenerateNumberAsync()
    {
        var next = await _context.SupplierPayments.CountAsync() + 1;
        return $"SP-{DateTime.Today:yyyy}-{next:00000}";
    }
}

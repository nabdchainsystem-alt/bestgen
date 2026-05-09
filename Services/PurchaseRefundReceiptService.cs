using bestgen.Data;
using bestgen.Models;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services;

/// <summary>
/// Posts a purchase refund: a supplier refunds us for returned goods.
///   DR Cash (1000) or Bank (1010) - cash flowing back to us.
///   CR Purchase Returns (5900) - reduces COGS / purchases.
/// Supplier balance is recalculated post-commit.
/// </summary>
public class PurchaseRefundReceiptService
{
    private readonly ApplicationDbContext _context;
    private readonly AccountingService _accounting;
    private readonly ChartOfAccounts _chart;
    private readonly PartyBalanceService _partyBalance;

    public PurchaseRefundReceiptService(
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

    public async Task PrepareAsync(PurchaseRefundReceipt refund)
    {
        if (string.IsNullOrWhiteSpace(refund.RefundNumber))
        {
            refund.RefundNumber = await GenerateNumberAsync();
        }
    }

    public async Task PostAsync(PurchaseRefundReceipt refund)
    {
        if (refund.Status != ReceiptStatus.Confirmed || refund.Amount <= 0)
        {
            return;
        }

        var debitCode = refund.RefundMethod == PaymentMethod.Bank
            ? AccountCodes.Bank
            : AccountCodes.Cash;

        var cashOrBank = await _chart.ResolveAsync(debitCode);
        var purchaseReturns = await _chart.ResolveAsync(AccountCodes.PurchaseReturns);

        await _accounting.BuildAndAddEntryAsync(
            entryDate: refund.Date,
            sourceModule: nameof(PurchaseRefundReceipt),
            description: $"Purchase refund {refund.RefundNumber}",
            lines: new[]
            {
                new JournalEntryLine { AccountId = cashOrBank.Id, Debit = refund.Amount, Credit = 0, Description = "Cash refund received" },
                new JournalEntryLine { AccountId = purchaseReturns.Id, Debit = 0, Credit = refund.Amount, Description = "Purchase returns" }
            });
    }

    public async Task ApplyAsync(PurchaseRefundReceipt refund)
    {
        await _partyBalance.RecalculateSupplierAsync(refund.SupplierId);
    }

    private async Task<string> GenerateNumberAsync()
    {
        var next = await _context.PurchaseRefundReceipts.CountAsync() + 1;
        return $"PRR-{DateTime.Today:yyyy}-{next:00000}";
    }
}

using bestgen.Data;
using bestgen.Models;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services;

/// <summary>
/// Posts a sales refund: cash flowing back out to a customer who returned goods.
///   DR Sales Returns (4900)  - reverses the original revenue.
///   CR Cash (1000) or Bank (1010) - cash actually paid out.
/// Customer balance is recalculated post-commit so any prior receipts/refunds settle.
/// </summary>
public class SalesRefundReceiptService
{
    private readonly ApplicationDbContext _context;
    private readonly AccountingService _accounting;
    private readonly ChartOfAccounts _chart;
    private readonly PartyBalanceService _partyBalance;

    public SalesRefundReceiptService(
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

    public async Task PrepareAsync(SalesRefundReceipt refund)
    {
        if (string.IsNullOrWhiteSpace(refund.RefundNumber))
        {
            refund.RefundNumber = await GenerateNumberAsync();
        }
    }

    public async Task PostAsync(SalesRefundReceipt refund)
    {
        if (refund.Status != ReceiptStatus.Confirmed || refund.Amount <= 0)
        {
            return;
        }

        var creditCode = refund.RefundMethod == PaymentMethod.Bank
            ? AccountCodes.Bank
            : AccountCodes.Cash;

        var salesReturns = await _chart.ResolveAsync(AccountCodes.SalesReturns);
        var cashOrBank = await _chart.ResolveAsync(creditCode);

        await _accounting.BuildAndAddEntryAsync(
            entryDate: refund.Date,
            sourceModule: nameof(SalesRefundReceipt),
            description: $"Sales refund {refund.RefundNumber}",
            lines: new[]
            {
                new JournalEntryLine { AccountId = salesReturns.Id, Debit = refund.Amount, Credit = 0, Description = "Sales returns" },
                new JournalEntryLine { AccountId = cashOrBank.Id, Debit = 0, Credit = refund.Amount, Description = "Cash refund" }
            });
    }

    public async Task ApplyAsync(SalesRefundReceipt refund)
    {
        await _partyBalance.RecalculateCustomerAsync(refund.CustomerId);
    }

    private async Task<string> GenerateNumberAsync()
    {
        var next = await _context.SalesRefundReceipts.CountAsync() + 1;
        return $"SRR-{DateTime.Today:yyyy}-{next:00000}";
    }
}

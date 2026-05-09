using bestgen.Data;
using bestgen.Models;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services;

/// <summary>
/// Recomputes the running balance of customers and suppliers from primary documents.
/// Called by every transactional service after it commits a posting so the
/// CurrentBalance field stays authoritative for dashboards and statements.
///
/// Convention: positive balance means the party owes us (AR for customers,
/// negative-debt for suppliers). For suppliers we store the amount we owe them.
///
/// Like other services in this layer it does not call SaveChanges itself —
/// the caller commits the unit of work.
/// </summary>
public class PartyBalanceService
{
    private readonly ApplicationDbContext _context;

    public PartyBalanceService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task RecalculateCustomerAsync(int customerId)
    {
        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == customerId);
        if (customer is null)
        {
            return;
        }

        // Sum issued invoices excluding cancelled drafts so a stale draft never inflates AR.
        var invoiced = await _context.SalesInvoices
            .Where(i => i.CustomerId == customerId && i.Status != InvoiceStatus.Draft && i.Status != InvoiceStatus.Cancelled)
            .SumAsync(i => (decimal?)i.GrandTotal) ?? 0m;

        var receipts = await _context.SalesReceipts
            .Where(r => r.CustomerId == customerId && r.Status == ReceiptStatus.Confirmed)
            .SumAsync(r => (decimal?)r.Amount) ?? 0m;

        var refunds = await _context.SalesRefundReceipts
            .Where(r => r.CustomerId == customerId && r.Status == ReceiptStatus.Confirmed)
            .SumAsync(r => (decimal?)r.Amount) ?? 0m;

        var credits = await _context.CreditNotes
            .Where(c => c.CustomerId == customerId && c.Status == CreditNoteStatus.Issued)
            .SumAsync(c => (decimal?)(c.Amount + c.VatAmount)) ?? 0m;

        // CurrentBalance = openingBalance + invoiced - receipts - credits + refunds-paid-by-us-back
        // Refunds paid back to the customer reduce their owing balance.
        customer.CurrentBalance = customer.OpeningBalance + invoiced - receipts - credits - refunds;
        customer.UpdatedAt = DateTime.UtcNow;
    }

    public async Task RecalculateSupplierAsync(int supplierId)
    {
        var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.Id == supplierId);
        if (supplier is null)
        {
            return;
        }

        var billed = await _context.PurchaseInvoices
            .Where(i => i.SupplierId == supplierId && i.Status != PurchaseInvoiceStatus.Draft && i.Status != PurchaseInvoiceStatus.Cancelled)
            .SumAsync(i => (decimal?)i.GrandTotal) ?? 0m;

        var payments = await _context.SupplierPayments
            .Where(p => p.SupplierId == supplierId && p.Status == ReceiptStatus.Confirmed)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        var refunds = await _context.PurchaseRefundReceipts
            .Where(r => r.SupplierId == supplierId && r.Status == ReceiptStatus.Confirmed)
            .SumAsync(r => (decimal?)r.Amount) ?? 0m;

        var debits = await _context.DebitNotes
            .Where(d => d.SupplierId == supplierId && d.Status == CreditNoteStatus.Issued)
            .SumAsync(d => (decimal?)(d.Amount + d.VatAmount)) ?? 0m;

        // CurrentBalance for a supplier = how much we owe them.
        supplier.CurrentBalance = supplier.OpeningBalance + billed - payments - debits - refunds;
        supplier.UpdatedAt = DateTime.UtcNow;
    }
}

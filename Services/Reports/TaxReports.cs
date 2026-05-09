using System.Globalization;
using bestgen.Data;
using bestgen.Models;
using bestgen.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services.Reports;

/// <summary>
/// VAT-driven reports. The current period's output and input VAT are summed
/// from the VAT lines on sales and purchase invoices respectively.
/// </summary>
public class TaxReports
{
    private readonly ApplicationDbContext _context;

    public TaxReports(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ReportResult> VatReturnAsync(ReportFilters filters)
    {
        var (from, to) = ResolvePeriod(filters);

        var sales = await _context.SalesInvoices.AsNoTracking()
            .Where(i => i.Status != InvoiceStatus.Draft && i.Status != InvoiceStatus.Cancelled)
            .Where(i => i.InvoiceDate >= from && i.InvoiceDate <= to)
            .ToListAsync();

        var purchases = await _context.PurchaseInvoices.AsNoTracking()
            .Where(i => i.Status != PurchaseInvoiceStatus.Draft && i.Status != PurchaseInvoiceStatus.Cancelled)
            .Where(i => i.InvoiceDate >= from && i.InvoiceDate <= to)
            .ToListAsync();

        var creditNotes = await _context.CreditNotes.AsNoTracking()
            .Where(n => n.Status == CreditNoteStatus.Issued)
            .Where(n => n.Date >= from && n.Date <= to)
            .ToListAsync();

        var debitNotes = await _context.DebitNotes.AsNoTracking()
            .Where(n => n.Status == CreditNoteStatus.Issued)
            .Where(n => n.Date >= from && n.Date <= to)
            .ToListAsync();

        var outputNet = sales.Sum(i => i.Subtotal - i.DiscountTotal);
        var outputVat = sales.Sum(i => i.VatTotal);
        var inputNet = purchases.Sum(i => i.Subtotal - i.DiscountTotal);
        var inputVat = purchases.Sum(i => i.VatTotal);
        var creditNet = creditNotes.Sum(n => n.Amount);
        var creditVat = creditNotes.Sum(n => n.VatAmount);
        var debitNet = debitNotes.Sum(n => n.Amount);
        var debitVat = debitNotes.Sum(n => n.VatAmount);

        var netVatOut = outputVat - creditVat;
        var netVatIn = inputVat - debitVat;
        var vatPayable = netVatOut - netVatIn;

        var rows = new List<ReportRow>
        {
            Row("1", "Sales (taxable supplies)", "المبيعات الخاضعة للضريبة", outputNet, outputVat),
            Row("2", "Sales returns / credit notes", "مردودات المبيعات / إشعارات دائنة", -creditNet, -creditVat),
            Row("3", "Net output VAT", "صافي ضريبة المخرجات", outputNet - creditNet, netVatOut, isTotal: true),
            Row("4", "Purchases (taxable supplies)", "المشتريات الخاضعة للضريبة", inputNet, inputVat),
            Row("5", "Purchase returns / debit notes", "مردودات المشتريات / إشعارات مدينة", -debitNet, -debitVat),
            Row("6", "Net input VAT", "صافي ضريبة المدخلات", inputNet - debitNet, netVatIn, isTotal: true)
        };

        return new ReportResult(
            ReportKey: "vat-return",
            TitleEn: "VAT Return",
            TitleAr: "الإقرار الضريبي",
            Columns: new[]
            {
                new ReportColumn("line", "Line", "البند"),
                new ReportColumn("labelEn", "Description (EN)", "الوصف (EN)"),
                new ReportColumn("labelAr", "Description (AR)", "الوصف (AR)"),
                new ReportColumn("net", "Net amount", "صافي المبلغ", "end", true),
                new ReportColumn("vat", "VAT amount", "قيمة الضريبة", "end", true)
            },
            Rows: rows,
            Totals: new[]
            {
                new ReportTotal("VAT payable / receivable", "الضريبة المستحقة / المستردة", Money(vatPayable), IsMoney: true)
            },
            Filters: filters);
    }

    private static ReportRow Row(string line, string labelEn, string labelAr, decimal net, decimal vat, bool isTotal = false)
        => new(new Dictionary<string, string?>
        {
            ["line"] = line,
            ["labelEn"] = labelEn,
            ["labelAr"] = labelAr,
            ["net"] = Money(net),
            ["vat"] = Money(vat)
        }, Style: isTotal ? "total" : null);

    private static (DateTime from, DateTime to) ResolvePeriod(ReportFilters filters)
    {
        var to = filters.ToDate ?? DateTime.Today;
        var from = filters.FromDate ?? new DateTime(to.Year, to.Month, 1);
        if (from > to) (from, to) = (to, from);
        return (from, to);
    }

    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}

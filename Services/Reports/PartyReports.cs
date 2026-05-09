using System.Globalization;
using bestgen.Data;
using bestgen.Models;
using bestgen.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services.Reports;

/// <summary>
/// Customer- and supplier-driven reports: statements, balance summaries, aging.
/// Statements walk source documents (invoices/receipts/credits) so the running
/// balance shown matches what the ledger sees.
/// </summary>
public class PartyReports
{
    private readonly ApplicationDbContext _context;

    public PartyReports(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ReportResult> CustomerStatementAsync(ReportFilters filters)
    {
        var (from, to) = ResolvePeriod(filters);

        var query = _context.Customers.AsNoTracking().AsQueryable();
        if (filters.CustomerId is int cid) query = query.Where(c => c.Id == cid);
        var customers = await query.OrderBy(c => c.NameAr).ToListAsync();

        var rows = new List<ReportRow>();
        decimal grandRunning = 0m;

        foreach (var customer in customers)
        {
            var invoices = await _context.SalesInvoices.AsNoTracking()
                .Where(i => i.CustomerId == customer.Id
                         && i.Status != InvoiceStatus.Draft
                         && i.Status != InvoiceStatus.Cancelled
                         && i.InvoiceDate >= from && i.InvoiceDate <= to)
                .ToListAsync();

            var receipts = await _context.SalesReceipts.AsNoTracking()
                .Where(r => r.CustomerId == customer.Id
                         && r.Status == ReceiptStatus.Confirmed
                         && r.Date >= from && r.Date <= to)
                .ToListAsync();

            var credits = await _context.CreditNotes.AsNoTracking()
                .Where(n => n.CustomerId == customer.Id
                         && n.Status == CreditNoteStatus.Issued
                         && n.Date >= from && n.Date <= to)
                .ToListAsync();

            var events = invoices.Select(i => new
            {
                i.InvoiceDate, Number = i.InvoiceNumber,
                Description = "Sales invoice / فاتورة مبيعات",
                Debit = i.GrandTotal, Credit = 0m
            })
            .Concat(receipts.Select(r => new
            {
                InvoiceDate = r.Date, Number = r.ReceiptNumber,
                Description = "Receipt / إيصال قبض",
                Debit = 0m, Credit = r.Amount
            }))
            .Concat(credits.Select(n => new
            {
                InvoiceDate = n.Date, Number = n.CreditNoteNumber,
                Description = "Credit note / إشعار دائن",
                Debit = 0m, Credit = n.Amount + n.VatAmount
            }))
            .OrderBy(e => e.InvoiceDate)
            .ThenBy(e => e.Number);

            if (!events.Any()) continue;

            rows.Add(Group($"{customer.CustomerCode} — {customer.NameAr}"));
            decimal running = customer.OpeningBalance;
            rows.Add(new ReportRow(new Dictionary<string, string?>
            {
                ["date"] = "-", ["number"] = "Opening / افتتاحي",
                ["description"] = "-", ["debit"] = "-", ["credit"] = "-",
                ["balance"] = Money(running)
            }));

            foreach (var ev in events)
            {
                running += ev.Debit - ev.Credit;
                rows.Add(new ReportRow(new Dictionary<string, string?>
                {
                    ["date"] = ev.InvoiceDate.ToString("yyyy-MM-dd"),
                    ["number"] = ev.Number,
                    ["description"] = ev.Description,
                    ["debit"] = Money(ev.Debit),
                    ["credit"] = Money(ev.Credit),
                    ["balance"] = Money(running)
                }));
            }
            grandRunning += running;
        }

        return new ReportResult(
            ReportKey: "customer-statement",
            TitleEn: "Customer Statement",
            TitleAr: "كشف عميل",
            Columns: new[]
            {
                new ReportColumn("date", "Date", "التاريخ"),
                new ReportColumn("number", "Reference", "المرجع"),
                new ReportColumn("description", "Description", "الوصف"),
                new ReportColumn("debit", "Debit", "مدين", "end", true),
                new ReportColumn("credit", "Credit", "دائن", "end", true),
                new ReportColumn("balance", "Balance", "الرصيد", "end", true)
            },
            Rows: rows,
            Totals: new[]
            {
                new ReportTotal("Closing balance", "الرصيد الختامي", Money(grandRunning), IsMoney: true)
            },
            Filters: filters);
    }

    public async Task<ReportResult> SupplierStatementAsync(ReportFilters filters)
    {
        var (from, to) = ResolvePeriod(filters);

        var query = _context.Suppliers.AsNoTracking().AsQueryable();
        if (filters.SupplierId is int sid) query = query.Where(s => s.Id == sid);
        var suppliers = await query.OrderBy(s => s.NameAr).ToListAsync();

        var rows = new List<ReportRow>();
        decimal grandRunning = 0m;

        foreach (var supplier in suppliers)
        {
            var invoices = await _context.PurchaseInvoices.AsNoTracking()
                .Where(i => i.SupplierId == supplier.Id
                         && i.Status != PurchaseInvoiceStatus.Draft
                         && i.Status != PurchaseInvoiceStatus.Cancelled
                         && i.InvoiceDate >= from && i.InvoiceDate <= to)
                .ToListAsync();

            var payments = await _context.SupplierPayments.AsNoTracking()
                .Where(p => p.SupplierId == supplier.Id
                         && p.Status == ReceiptStatus.Confirmed
                         && p.Date >= from && p.Date <= to)
                .ToListAsync();

            var debits = await _context.DebitNotes.AsNoTracking()
                .Where(n => n.SupplierId == supplier.Id
                         && n.Status == CreditNoteStatus.Issued
                         && n.Date >= from && n.Date <= to)
                .ToListAsync();

            var events = invoices.Select(i => new
            {
                i.InvoiceDate, Number = i.PurchaseInvoiceNumber,
                Description = "Purchase invoice / فاتورة مشتريات",
                Debit = 0m, Credit = i.GrandTotal
            })
            .Concat(payments.Select(p => new
            {
                InvoiceDate = p.Date, Number = p.PaymentNumber,
                Description = "Payment / إيصال دفع",
                Debit = p.Amount, Credit = 0m
            }))
            .Concat(debits.Select(n => new
            {
                InvoiceDate = n.Date, Number = n.DebitNoteNumber,
                Description = "Debit note / إشعار مدين",
                Debit = n.Amount + n.VatAmount, Credit = 0m
            }))
            .OrderBy(e => e.InvoiceDate)
            .ThenBy(e => e.Number);

            if (!events.Any()) continue;

            rows.Add(Group($"{supplier.SupplierCode} — {supplier.NameAr}"));
            decimal running = supplier.OpeningBalance;
            rows.Add(new ReportRow(new Dictionary<string, string?>
            {
                ["date"] = "-", ["number"] = "Opening / افتتاحي",
                ["description"] = "-", ["debit"] = "-", ["credit"] = "-",
                ["balance"] = Money(running)
            }));

            foreach (var ev in events)
            {
                running += ev.Credit - ev.Debit;
                rows.Add(new ReportRow(new Dictionary<string, string?>
                {
                    ["date"] = ev.InvoiceDate.ToString("yyyy-MM-dd"),
                    ["number"] = ev.Number,
                    ["description"] = ev.Description,
                    ["debit"] = Money(ev.Debit),
                    ["credit"] = Money(ev.Credit),
                    ["balance"] = Money(running)
                }));
            }
            grandRunning += running;
        }

        return new ReportResult(
            ReportKey: "supplier-statement",
            TitleEn: "Supplier Statement",
            TitleAr: "كشف مورد",
            Columns: new[]
            {
                new ReportColumn("date", "Date", "التاريخ"),
                new ReportColumn("number", "Reference", "المرجع"),
                new ReportColumn("description", "Description", "الوصف"),
                new ReportColumn("debit", "Debit", "مدين", "end", true),
                new ReportColumn("credit", "Credit", "دائن", "end", true),
                new ReportColumn("balance", "Balance", "الرصيد", "end", true)
            },
            Rows: rows,
            Totals: new[]
            {
                new ReportTotal("Closing balance", "الرصيد الختامي", Money(grandRunning), IsMoney: true)
            },
            Filters: filters);
    }

    public async Task<ReportResult> CustomerBalancesAsync(ReportFilters filters)
    {
        var customers = await _context.Customers.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderByDescending(c => c.CurrentBalance)
            .ToListAsync();

        var rows = customers.Select(c => new ReportRow(new Dictionary<string, string?>
        {
            ["code"] = c.CustomerCode,
            ["name"] = c.NameAr,
            ["phone"] = c.Phone ?? "-",
            ["limit"] = Money(c.CreditLimit),
            ["balance"] = Money(c.CurrentBalance)
        })).ToList();

        return new ReportResult(
            ReportKey: "customer-balances-summary",
            TitleEn: "Customer Balances",
            TitleAr: "ملخص أرصدة العملاء",
            Columns: new[]
            {
                new ReportColumn("code", "Code", "الكود"),
                new ReportColumn("name", "Customer", "العميل"),
                new ReportColumn("phone", "Phone", "الهاتف"),
                new ReportColumn("limit", "Credit limit", "حد الائتمان", "end", true),
                new ReportColumn("balance", "Balance", "الرصيد", "end", true)
            },
            Rows: rows,
            Totals: new[]
            {
                new ReportTotal("Total receivable", "إجمالي المستحق", Money(customers.Sum(c => c.CurrentBalance)), IsMoney: true)
            },
            Filters: filters);
    }

    public async Task<ReportResult> SupplierBalancesAsync(ReportFilters filters)
    {
        var suppliers = await _context.Suppliers.AsNoTracking()
            .Where(s => s.IsActive)
            .OrderByDescending(s => s.CurrentBalance)
            .ToListAsync();

        var rows = suppliers.Select(s => new ReportRow(new Dictionary<string, string?>
        {
            ["code"] = s.SupplierCode,
            ["name"] = s.NameAr,
            ["phone"] = s.Phone ?? "-",
            ["balance"] = Money(s.CurrentBalance)
        })).ToList();

        return new ReportResult(
            ReportKey: "supplier-balances-summary",
            TitleEn: "Supplier Balances",
            TitleAr: "ملخص أرصدة الموردين",
            Columns: new[]
            {
                new ReportColumn("code", "Code", "الكود"),
                new ReportColumn("name", "Supplier", "المورد"),
                new ReportColumn("phone", "Phone", "الهاتف"),
                new ReportColumn("balance", "Balance", "الرصيد", "end", true)
            },
            Rows: rows,
            Totals: new[]
            {
                new ReportTotal("Total payable", "إجمالي المستحق", Money(suppliers.Sum(s => s.CurrentBalance)), IsMoney: true)
            },
            Filters: filters);
    }

    public async Task<ReportResult> AgingReceivablesAsync(ReportFilters filters)
    {
        var asOf = filters.ToDate ?? DateTime.Today;

        var invoices = await _context.SalesInvoices.AsNoTracking()
            .Include(i => i.Customer)
            .Where(i => i.Status != InvoiceStatus.Draft && i.Status != InvoiceStatus.Cancelled && i.RemainingAmount > 0)
            .ToListAsync();

        var rows = invoices.GroupBy(i => i.Customer)
            .Select(g =>
            {
                var current = 0m; var b30 = 0m; var b60 = 0m; var b90 = 0m; var b120 = 0m;
                foreach (var inv in g)
                {
                    var days = (asOf - inv.InvoiceDate).Days;
                    if (days <= 30) current += inv.RemainingAmount;
                    else if (days <= 60) b30 += inv.RemainingAmount;
                    else if (days <= 90) b60 += inv.RemainingAmount;
                    else if (days <= 120) b90 += inv.RemainingAmount;
                    else b120 += inv.RemainingAmount;
                }
                return new ReportRow(new Dictionary<string, string?>
                {
                    ["customer"] = g.Key?.NameAr ?? "-",
                    ["current"] = Money(current),
                    ["b30"] = Money(b30),
                    ["b60"] = Money(b60),
                    ["b90"] = Money(b90),
                    ["b120"] = Money(b120),
                    ["total"] = Money(current + b30 + b60 + b90 + b120)
                });
            })
            .OrderByDescending(r => decimal.Parse(r.Cells["total"] ?? "0", CultureInfo.InvariantCulture))
            .ToList();

        return new ReportResult(
            ReportKey: "receivables-aging",
            TitleEn: "Aging Receivables",
            TitleAr: "أعمار ديون المبيعات",
            Columns: new[]
            {
                new ReportColumn("customer", "Customer", "العميل"),
                new ReportColumn("current", "0-30", "0-30", "end", true),
                new ReportColumn("b30", "31-60", "31-60", "end", true),
                new ReportColumn("b60", "61-90", "61-90", "end", true),
                new ReportColumn("b90", "91-120", "91-120", "end", true),
                new ReportColumn("b120", "120+", "120+", "end", true),
                new ReportColumn("total", "Total", "الإجمالي", "end", true)
            },
            Rows: rows,
            Totals: Array.Empty<ReportTotal>(),
            Filters: filters);
    }

    public async Task<ReportResult> AgingPayablesAsync(ReportFilters filters)
    {
        var asOf = filters.ToDate ?? DateTime.Today;

        var invoices = await _context.PurchaseInvoices.AsNoTracking()
            .Include(i => i.Supplier)
            .Where(i => i.Status != PurchaseInvoiceStatus.Draft && i.Status != PurchaseInvoiceStatus.Cancelled && i.RemainingAmount > 0)
            .ToListAsync();

        var rows = invoices.GroupBy(i => i.Supplier)
            .Select(g =>
            {
                var current = 0m; var b30 = 0m; var b60 = 0m; var b90 = 0m; var b120 = 0m;
                foreach (var inv in g)
                {
                    var days = (asOf - inv.InvoiceDate).Days;
                    if (days <= 30) current += inv.RemainingAmount;
                    else if (days <= 60) b30 += inv.RemainingAmount;
                    else if (days <= 90) b60 += inv.RemainingAmount;
                    else if (days <= 120) b90 += inv.RemainingAmount;
                    else b120 += inv.RemainingAmount;
                }
                return new ReportRow(new Dictionary<string, string?>
                {
                    ["supplier"] = g.Key?.NameAr ?? "-",
                    ["current"] = Money(current),
                    ["b30"] = Money(b30),
                    ["b60"] = Money(b60),
                    ["b90"] = Money(b90),
                    ["b120"] = Money(b120),
                    ["total"] = Money(current + b30 + b60 + b90 + b120)
                });
            })
            .OrderByDescending(r => decimal.Parse(r.Cells["total"] ?? "0", CultureInfo.InvariantCulture))
            .ToList();

        return new ReportResult(
            ReportKey: "payables-aging",
            TitleEn: "Aging Payables",
            TitleAr: "أعمار ديون المشتريات",
            Columns: new[]
            {
                new ReportColumn("supplier", "Supplier", "المورد"),
                new ReportColumn("current", "0-30", "0-30", "end", true),
                new ReportColumn("b30", "31-60", "31-60", "end", true),
                new ReportColumn("b60", "61-90", "61-90", "end", true),
                new ReportColumn("b90", "91-120", "91-120", "end", true),
                new ReportColumn("b120", "120+", "120+", "end", true),
                new ReportColumn("total", "Total", "الإجمالي", "end", true)
            },
            Rows: rows,
            Totals: Array.Empty<ReportTotal>(),
            Filters: filters);
    }

    private static (DateTime from, DateTime to) ResolvePeriod(ReportFilters filters)
    {
        var to = filters.ToDate ?? DateTime.Today;
        var from = filters.FromDate ?? new DateTime(to.Year, to.Month, 1);
        if (from > to) (from, to) = (to, from);
        return (from, to);
    }

    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private static ReportRow Group(string label) => new(
        new Dictionary<string, string?> { ["__group__"] = label },
        Style: "group");
}

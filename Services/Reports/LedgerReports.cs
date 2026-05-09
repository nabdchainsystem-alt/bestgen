using System.Globalization;
using bestgen.Data;
using bestgen.Models;
using bestgen.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services.Reports;

/// <summary>
/// Reports driven directly off the general ledger (JournalEntry + JournalEntryLine + Account).
/// Posted-only entries are included; drafts and cancelled entries are excluded so reports
/// reflect what actually hit the books.
/// </summary>
public class LedgerReports
{
    private readonly ApplicationDbContext _context;

    public LedgerReports(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ReportResult> GeneralLedgerAsync(ReportFilters filters)
    {
        var (from, to) = ResolvePeriod(filters);

        var query = _context.JournalEntryLines
            .AsNoTracking()
            .Include(l => l.Account)
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry!.Status == JournalEntryStatus.Posted)
            .Where(l => l.JournalEntry!.EntryDate >= from && l.JournalEntry!.EntryDate <= to);

        if (filters.AccountId is int aid)
        {
            query = query.Where(l => l.AccountId == aid);
        }

        var lines = await query
            .OrderBy(l => l.Account!.AccountCode)
            .ThenBy(l => l.JournalEntry!.EntryDate)
            .ThenBy(l => l.Id)
            .ToListAsync();

        var rows = new List<ReportRow>();
        decimal totalDebit = 0, totalCredit = 0;

        foreach (var group in lines.GroupBy(l => new { l.AccountId, l.Account!.AccountCode, l.Account.AccountNameAr, l.Account.AccountNameEn }))
        {
            rows.Add(Group($"{group.Key.AccountCode} — {group.Key.AccountNameAr}"));
            decimal running = 0m;
            foreach (var line in group)
            {
                running += line.Debit - line.Credit;
                totalDebit += line.Debit;
                totalCredit += line.Credit;
                rows.Add(new ReportRow(new Dictionary<string, string?>
                {
                    ["date"] = line.JournalEntry!.EntryDate.ToString("yyyy-MM-dd"),
                    ["entry"] = line.JournalEntry.EntryNumber,
                    ["description"] = line.Description ?? line.JournalEntry.Description,
                    ["debit"] = Money(line.Debit),
                    ["credit"] = Money(line.Credit),
                    ["balance"] = Money(running)
                }));
            }
        }

        return new ReportResult(
            ReportKey: "general-ledger",
            TitleEn: "General Ledger",
            TitleAr: "دفتر الأستاذ",
            Columns: new[]
            {
                new ReportColumn("date", "Date", "التاريخ"),
                new ReportColumn("entry", "Entry", "رقم القيد"),
                new ReportColumn("description", "Description", "الوصف"),
                new ReportColumn("debit", "Debit", "مدين", "end", true),
                new ReportColumn("credit", "Credit", "دائن", "end", true),
                new ReportColumn("balance", "Balance", "الرصيد", "end", true)
            },
            Rows: rows,
            Totals: new[]
            {
                new ReportTotal("Total debits", "إجمالي المدين", Money(totalDebit), IsMoney: true),
                new ReportTotal("Total credits", "إجمالي الدائن", Money(totalCredit), IsMoney: true)
            },
            Filters: filters);
    }

    public async Task<ReportResult> TrialBalanceAsync(ReportFilters filters)
    {
        var (from, to) = ResolvePeriod(filters);

        var lines = await _context.JournalEntryLines
            .AsNoTracking()
            .Include(l => l.Account)
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry!.Status == JournalEntryStatus.Posted)
            .Where(l => l.JournalEntry!.EntryDate >= from && l.JournalEntry!.EntryDate <= to)
            .ToListAsync();

        var rows = new List<ReportRow>();
        decimal totalDebit = 0, totalCredit = 0;

        var accounts = await _context.Accounts.AsNoTracking()
            .Where(a => a.IsActive)
            .OrderBy(a => a.AccountCode)
            .ToListAsync();

        foreach (var account in accounts)
        {
            var accountLines = lines.Where(l => l.AccountId == account.Id).ToList();
            if (accountLines.Count == 0) continue;

            var debit = accountLines.Sum(l => l.Debit);
            var credit = accountLines.Sum(l => l.Credit);
            var net = debit - credit;
            totalDebit += debit;
            totalCredit += credit;

            rows.Add(new ReportRow(new Dictionary<string, string?>
            {
                ["code"] = account.AccountCode,
                ["name"] = account.AccountNameAr,
                ["type"] = account.AccountType.ToString(),
                ["debit"] = net > 0 ? Money(net) : "0.00",
                ["credit"] = net < 0 ? Money(-net) : "0.00"
            }));
        }

        return new ReportResult(
            ReportKey: "trial-balance",
            TitleEn: "Trial Balance",
            TitleAr: "ميزان المراجعة",
            Columns: new[]
            {
                new ReportColumn("code", "Code", "الكود"),
                new ReportColumn("name", "Account", "الحساب"),
                new ReportColumn("type", "Type", "النوع"),
                new ReportColumn("debit", "Debit balance", "رصيد مدين", "end", true),
                new ReportColumn("credit", "Credit balance", "رصيد دائن", "end", true)
            },
            Rows: rows,
            Totals: new[]
            {
                new ReportTotal("Total debits", "إجمالي المدين",
                    Money(rows.Sum(r => decimal.Parse(r.Cells["debit"] ?? "0", CultureInfo.InvariantCulture))), IsMoney: true),
                new ReportTotal("Total credits", "إجمالي الدائن",
                    Money(rows.Sum(r => decimal.Parse(r.Cells["credit"] ?? "0", CultureInfo.InvariantCulture))), IsMoney: true)
            },
            Filters: filters);
    }

    public async Task<ReportResult> IncomeStatementAsync(ReportFilters filters)
    {
        var (from, to) = ResolvePeriod(filters);

        var data = await _context.JournalEntryLines
            .AsNoTracking()
            .Include(l => l.Account)
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry!.Status == JournalEntryStatus.Posted)
            .Where(l => l.JournalEntry!.EntryDate >= from && l.JournalEntry!.EntryDate <= to)
            .Where(l => l.Account!.AccountType == AccountType.Revenue || l.Account.AccountType == AccountType.Expense)
            .ToListAsync();

        var rows = new List<ReportRow>();
        decimal revenueTotal = 0m, expenseTotal = 0m;

        rows.Add(Group("Revenue / الإيرادات"));
        foreach (var group in data.Where(l => l.Account!.AccountType == AccountType.Revenue)
            .GroupBy(l => new { l.AccountId, l.Account!.AccountCode, l.Account.AccountNameAr }))
        {
            var amount = group.Sum(l => l.Credit - l.Debit);
            revenueTotal += amount;
            rows.Add(new ReportRow(new Dictionary<string, string?>
            {
                ["account"] = $"{group.Key.AccountCode} — {group.Key.AccountNameAr}",
                ["amount"] = Money(amount)
            }));
        }
        rows.Add(new ReportRow(new Dictionary<string, string?>
        {
            ["account"] = "Total revenue / إجمالي الإيرادات",
            ["amount"] = Money(revenueTotal)
        }, Style: "total"));

        rows.Add(Group("Expenses / المصروفات"));
        foreach (var group in data.Where(l => l.Account!.AccountType == AccountType.Expense)
            .GroupBy(l => new { l.AccountId, l.Account!.AccountCode, l.Account.AccountNameAr }))
        {
            var amount = group.Sum(l => l.Debit - l.Credit);
            expenseTotal += amount;
            rows.Add(new ReportRow(new Dictionary<string, string?>
            {
                ["account"] = $"{group.Key.AccountCode} — {group.Key.AccountNameAr}",
                ["amount"] = Money(amount)
            }));
        }
        rows.Add(new ReportRow(new Dictionary<string, string?>
        {
            ["account"] = "Total expenses / إجمالي المصروفات",
            ["amount"] = Money(expenseTotal)
        }, Style: "total"));

        return new ReportResult(
            ReportKey: "income-statement",
            TitleEn: "Income Statement",
            TitleAr: "قائمة الدخل",
            Columns: new[]
            {
                new ReportColumn("account", "Account", "الحساب"),
                new ReportColumn("amount", "Amount", "المبلغ", "end", true)
            },
            Rows: rows,
            Totals: new[]
            {
                new ReportTotal("Net income", "صافي الربح", Money(revenueTotal - expenseTotal), IsMoney: true)
            },
            Filters: filters);
    }

    public async Task<ReportResult> BalanceSheetAsync(ReportFilters filters)
    {
        // Balance sheet is point-in-time. Use ToDate as the as-of date; ignore FromDate.
        var asOf = filters.ToDate ?? DateTime.Today;

        var data = await _context.JournalEntryLines
            .AsNoTracking()
            .Include(l => l.Account)
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry!.Status == JournalEntryStatus.Posted)
            .Where(l => l.JournalEntry!.EntryDate <= asOf)
            .Where(l => l.Account!.AccountType == AccountType.Asset
                     || l.Account.AccountType == AccountType.Liability
                     || l.Account.AccountType == AccountType.Equity
                     || l.Account.AccountType == AccountType.Revenue
                     || l.Account.AccountType == AccountType.Expense)
            .ToListAsync();

        // Net income flows into retained earnings on the snapshot date.
        var netIncome = data
            .Where(l => l.Account!.AccountType == AccountType.Revenue || l.Account.AccountType == AccountType.Expense)
            .Sum(l => l.Account!.AccountType == AccountType.Revenue
                ? l.Credit - l.Debit
                : -(l.Debit - l.Credit));

        var rows = new List<ReportRow>();
        decimal assetsTotal = 0, liabilitiesTotal = 0, equityTotal = 0;

        rows.Add(Group("Assets / الأصول"));
        foreach (var group in data.Where(l => l.Account!.AccountType == AccountType.Asset)
            .GroupBy(l => new { l.AccountId, l.Account!.AccountCode, l.Account.AccountNameAr }))
        {
            var amount = group.Sum(l => l.Debit - l.Credit);
            assetsTotal += amount;
            rows.Add(new ReportRow(new Dictionary<string, string?>
            {
                ["account"] = $"{group.Key.AccountCode} — {group.Key.AccountNameAr}",
                ["amount"] = Money(amount)
            }));
        }
        rows.Add(TotalRow("Total assets / إجمالي الأصول", assetsTotal));

        rows.Add(Group("Liabilities / الخصوم"));
        foreach (var group in data.Where(l => l.Account!.AccountType == AccountType.Liability)
            .GroupBy(l => new { l.AccountId, l.Account!.AccountCode, l.Account.AccountNameAr }))
        {
            var amount = group.Sum(l => l.Credit - l.Debit);
            liabilitiesTotal += amount;
            rows.Add(new ReportRow(new Dictionary<string, string?>
            {
                ["account"] = $"{group.Key.AccountCode} — {group.Key.AccountNameAr}",
                ["amount"] = Money(amount)
            }));
        }
        rows.Add(TotalRow("Total liabilities / إجمالي الخصوم", liabilitiesTotal));

        rows.Add(Group("Equity / حقوق الملكية"));
        foreach (var group in data.Where(l => l.Account!.AccountType == AccountType.Equity)
            .GroupBy(l => new { l.AccountId, l.Account!.AccountCode, l.Account.AccountNameAr }))
        {
            var amount = group.Sum(l => l.Credit - l.Debit);
            equityTotal += amount;
            rows.Add(new ReportRow(new Dictionary<string, string?>
            {
                ["account"] = $"{group.Key.AccountCode} — {group.Key.AccountNameAr}",
                ["amount"] = Money(amount)
            }));
        }
        if (netIncome != 0)
        {
            equityTotal += netIncome;
            rows.Add(new ReportRow(new Dictionary<string, string?>
            {
                ["account"] = "Net income (current period) / صافي ربح الفترة",
                ["amount"] = Money(netIncome)
            }));
        }
        rows.Add(TotalRow("Total equity / إجمالي حقوق الملكية", equityTotal));

        return new ReportResult(
            ReportKey: "balance-sheet",
            TitleEn: "Balance Sheet",
            TitleAr: "الميزانية العمومية",
            Columns: new[]
            {
                new ReportColumn("account", "Account", "الحساب"),
                new ReportColumn("amount", "Amount", "المبلغ", "end", true)
            },
            Rows: rows,
            Totals: new[]
            {
                new ReportTotal("Assets", "الأصول", Money(assetsTotal), IsMoney: true),
                new ReportTotal("Liabilities + Equity", "الخصوم + حقوق الملكية", Money(liabilitiesTotal + equityTotal), IsMoney: true),
                new ReportTotal("Difference", "الفرق", Money(assetsTotal - (liabilitiesTotal + equityTotal)), IsMoney: true)
            },
            Filters: filters);
    }

    public async Task<ReportResult> AccountStatementAsync(ReportFilters filters)
    {
        // Simply the GL filtered by a single account.
        return await GeneralLedgerAsync(filters);
    }

    public async Task<ReportResult> GeneralReceiptsSummaryAsync(ReportFilters filters)
    {
        var (from, to) = ResolvePeriod(filters);

        var receipts = await _context.GeneralReceipts
            .AsNoTracking()
            .Include(r => r.Account)
            .Where(r => r.Date >= from && r.Date <= to && r.Status == ReceiptStatus.Confirmed)
            .OrderBy(r => r.Date)
            .ToListAsync();

        var rows = receipts.Select(r => new ReportRow(new Dictionary<string, string?>
        {
            ["date"] = r.Date.ToString("yyyy-MM-dd"),
            ["number"] = r.ReceiptNumber,
            ["type"] = r.ReceiptType.ToString(),
            ["account"] = r.Account is null ? "-" : $"{r.Account.AccountCode} — {r.Account.AccountNameAr}",
            ["amount"] = Money(r.ReceiptType == GeneralReceiptType.Receipt ? r.Amount : -r.Amount)
        })).ToList();

        var net = receipts.Sum(r => r.ReceiptType == GeneralReceiptType.Receipt ? r.Amount : -r.Amount);

        return new ReportResult(
            ReportKey: "general-receipts-summary",
            TitleEn: "General Receipts Summary",
            TitleAr: "ملخص الإيصالات العامة",
            Columns: new[]
            {
                new ReportColumn("date", "Date", "التاريخ"),
                new ReportColumn("number", "Number", "رقم الإيصال"),
                new ReportColumn("type", "Type", "النوع"),
                new ReportColumn("account", "Account", "الحساب"),
                new ReportColumn("amount", "Net amount", "صافي المبلغ", "end", true)
            },
            Rows: rows,
            Totals: new[]
            {
                new ReportTotal("Net amount", "صافي المبلغ", Money(net), IsMoney: true)
            },
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

    private static ReportRow TotalRow(string label, decimal value) => new(
        new Dictionary<string, string?> { ["account"] = label, ["amount"] = Money(value) },
        Style: "total");
}

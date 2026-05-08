using bestgen.Data;
using bestgen.Models;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services;

public class AccountingService
{
    private readonly ApplicationDbContext _context;

    public AccountingService(ApplicationDbContext context)
    {
        _context = context;
    }

    public bool IsBalanced(IEnumerable<JournalEntryLine> lines)
    {
        var totalDebit = lines.Sum(line => line.Debit);
        var totalCredit = lines.Sum(line => line.Credit);
        return totalDebit == totalCredit;
    }

    public async Task CreateSalesJournalEntryAsync(SalesInvoice invoice)
    {
        if (invoice.Status is InvoiceStatus.Draft or InvoiceStatus.Cancelled)
        {
            return;
        }

        var ar = await FindAccountAsync("1100");
        var revenue = await FindAccountAsync("4000");
        var vat = await FindAccountAsync("2100");
        if (ar is null || revenue is null || vat is null)
        {
            return;
        }

        var entry = new JournalEntry
        {
            EntryNumber = await GenerateEntryNumberAsync(),
            EntryDate = invoice.InvoiceDate,
            SourceModule = "SalesInvoice",
            Description = $"قيد مبيعات للفاتورة {invoice.InvoiceNumber}",
            Status = JournalEntryStatus.Draft,
            TotalDebit = invoice.GrandTotal,
            TotalCredit = invoice.GrandTotal,
            Lines =
            {
                new JournalEntryLine { AccountId = ar.Id, Debit = invoice.GrandTotal, Credit = 0, Description = "ذمم مدينة" },
                new JournalEntryLine { AccountId = revenue.Id, Debit = 0, Credit = invoice.Subtotal - invoice.DiscountTotal, Description = "إيراد مبيعات" },
                new JournalEntryLine { AccountId = vat.Id, Debit = 0, Credit = invoice.VatTotal, Description = "ضريبة مستحقة" }
            }
        };

        _context.JournalEntries.Add(entry);
    }

    public async Task CreatePurchaseJournalEntryAsync(PurchaseInvoice invoice)
    {
        if (invoice.Status is PurchaseInvoiceStatus.Draft or PurchaseInvoiceStatus.Cancelled)
        {
            return;
        }

        var inventory = await FindAccountAsync("1200");
        var vat = await FindAccountAsync("2100");
        var ap = await FindAccountAsync("2000");
        if (inventory is null || vat is null || ap is null)
        {
            return;
        }

        var entry = new JournalEntry
        {
            EntryNumber = await GenerateEntryNumberAsync(),
            EntryDate = invoice.InvoiceDate,
            SourceModule = "PurchaseInvoice",
            Description = $"قيد مشتريات للفاتورة {invoice.PurchaseInvoiceNumber}",
            Status = JournalEntryStatus.Draft,
            TotalDebit = invoice.GrandTotal,
            TotalCredit = invoice.GrandTotal,
            Lines =
            {
                new JournalEntryLine { AccountId = inventory.Id, Debit = invoice.Subtotal - invoice.DiscountTotal, Credit = 0, Description = "مخزون" },
                new JournalEntryLine { AccountId = vat.Id, Debit = invoice.VatTotal, Credit = 0, Description = "ضريبة مدخلات" },
                new JournalEntryLine { AccountId = ap.Id, Debit = 0, Credit = invoice.GrandTotal, Description = "ذمم دائنة" }
            }
        };

        _context.JournalEntries.Add(entry);
    }

    public async Task ApplyPaidExpenseAsync(Expense expense)
    {
        if (expense.Status != ExpenseStatus.Paid)
        {
            return;
        }

        if (expense.PaidFromType.Equals("Cash", StringComparison.OrdinalIgnoreCase) && expense.CashBoxId.HasValue)
        {
            var cashBox = await _context.CashBoxes.FindAsync(expense.CashBoxId.Value);
            if (cashBox is not null)
            {
                cashBox.CurrentBalance -= expense.TotalAmount;
                cashBox.UpdatedAt = DateTime.UtcNow;
            }
        }

        if (expense.PaidFromType.Equals("Bank", StringComparison.OrdinalIgnoreCase) && expense.BankAccountId.HasValue)
        {
            var bank = await _context.BankAccounts.FindAsync(expense.BankAccountId.Value);
            if (bank is not null)
            {
                bank.CurrentBalance -= expense.TotalAmount;
                bank.UpdatedAt = DateTime.UtcNow;
            }
        }

        var expenseAccount = await FindAccountAsync("5400");
        var cash = await FindAccountAsync(expense.PaidFromType.Equals("Bank", StringComparison.OrdinalIgnoreCase) ? "1010" : "1000");
        if (expenseAccount is null || cash is null)
        {
            return;
        }

        _context.JournalEntries.Add(new JournalEntry
        {
            EntryNumber = await GenerateEntryNumberAsync(),
            EntryDate = expense.ExpenseDate,
            SourceModule = "Expense",
            Description = $"قيد مصروف {expense.ExpenseNumber}",
            Status = JournalEntryStatus.Draft,
            TotalDebit = expense.TotalAmount,
            TotalCredit = expense.TotalAmount,
            Lines =
            {
                new JournalEntryLine { AccountId = expenseAccount.Id, Debit = expense.TotalAmount, Credit = 0, Description = expense.Category },
                new JournalEntryLine { AccountId = cash.Id, Debit = 0, Credit = expense.TotalAmount, Description = expense.PaidFromType }
            }
        });
    }

    private Task<Account?> FindAccountAsync(string code) =>
        _context.Accounts.FirstOrDefaultAsync(account => account.AccountCode == code);

    private async Task<string> GenerateEntryNumberAsync()
    {
        var next = await _context.JournalEntries.CountAsync() + 1;
        return $"JE-{next:00000}";
    }
}

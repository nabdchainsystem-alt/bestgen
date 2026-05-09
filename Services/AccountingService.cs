using bestgen.Data;
using bestgen.Models;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services;

/// <summary>
/// Posts journal entries for source documents and exposes a shared
/// <see cref="BuildAndAddEntryAsync"/> helper that every transaction service uses
/// to build a balanced, traceable entry.
///
/// Convention: <c>SourceModule</c> is the entity class name, <c>Description</c>
/// embeds the human-readable document number for quick search in the GL.
/// Like the rest of the orchestration layer, this service does not call
/// SaveChanges; the caller commits the unit of work.
/// </summary>
public class AccountingService
{
    private readonly ApplicationDbContext _context;
    private readonly ChartOfAccounts _chart;

    public AccountingService(ApplicationDbContext context, ChartOfAccounts chart)
    {
        _context = context;
        _chart = chart;
    }

    public bool IsBalanced(IEnumerable<JournalEntryLine> lines)
    {
        var totalDebit = lines.Sum(line => line.Debit);
        var totalCredit = lines.Sum(line => line.Credit);
        return totalDebit == totalCredit;
    }

    /// <summary>
    /// Builds a journal entry, validates it balances, attaches it to the context, and
    /// returns it so callers can hold a reference (e.g. to back-link from the source document).
    /// Throws if the lines do not balance — that is a programmer error, never user input.
    /// </summary>
    public async Task<JournalEntry> BuildAndAddEntryAsync(
        DateTime entryDate,
        string sourceModule,
        string description,
        IEnumerable<JournalEntryLine> lines,
        JournalEntryStatus status = JournalEntryStatus.Draft)
    {
        var lineList = lines.ToList();
        if (!IsBalanced(lineList))
        {
            var debits = lineList.Sum(l => l.Debit);
            var credits = lineList.Sum(l => l.Credit);
            throw new InvalidOperationException(
                $"Journal entry for {sourceModule} does not balance. Debits={debits}, credits={credits}.");
        }

        var entry = new JournalEntry
        {
            EntryNumber = await GenerateEntryNumberAsync(),
            EntryDate = entryDate,
            SourceModule = sourceModule,
            Description = description,
            Status = status,
            TotalDebit = lineList.Sum(l => l.Debit),
            TotalCredit = lineList.Sum(l => l.Credit),
            Lines = lineList
        };

        _context.JournalEntries.Add(entry);
        return entry;
    }

    public async Task CreateSalesJournalEntryAsync(SalesInvoice invoice)
    {
        if (invoice.Status is InvoiceStatus.Draft or InvoiceStatus.Cancelled)
        {
            return;
        }

        var ar = await _chart.ResolveAsync(AccountCodes.AccountsReceivable);
        var revenue = await _chart.ResolveAsync(AccountCodes.Sales);
        var vat = await _chart.ResolveAsync(AccountCodes.VatOutput);
        var netRevenue = invoice.Subtotal - invoice.DiscountTotal;

        await BuildAndAddEntryAsync(
            entryDate: invoice.InvoiceDate,
            sourceModule: nameof(SalesInvoice),
            description: $"Sales invoice {invoice.InvoiceNumber}",
            lines: new[]
            {
                new JournalEntryLine { AccountId = ar.Id, Debit = invoice.GrandTotal, Credit = 0, Description = "Accounts receivable" },
                new JournalEntryLine { AccountId = revenue.Id, Debit = 0, Credit = netRevenue, Description = "Sales revenue" },
                new JournalEntryLine { AccountId = vat.Id, Debit = 0, Credit = invoice.VatTotal, Description = "Output VAT" }
            });
    }

    public async Task CreatePurchaseJournalEntryAsync(PurchaseInvoice invoice)
    {
        if (invoice.Status is PurchaseInvoiceStatus.Draft or PurchaseInvoiceStatus.Cancelled)
        {
            return;
        }

        var inventory = await _chart.ResolveAsync(AccountCodes.Inventory);
        var vat = await _chart.ResolveAsync(AccountCodes.VatInput);
        var ap = await _chart.ResolveAsync(AccountCodes.AccountsPayable);
        var netCost = invoice.Subtotal - invoice.DiscountTotal;

        await BuildAndAddEntryAsync(
            entryDate: invoice.InvoiceDate,
            sourceModule: nameof(PurchaseInvoice),
            description: $"Purchase invoice {invoice.PurchaseInvoiceNumber}",
            lines: new[]
            {
                new JournalEntryLine { AccountId = inventory.Id, Debit = netCost, Credit = 0, Description = "Inventory" },
                new JournalEntryLine { AccountId = vat.Id, Debit = invoice.VatTotal, Credit = 0, Description = "Input VAT" },
                new JournalEntryLine { AccountId = ap.Id, Debit = 0, Credit = invoice.GrandTotal, Description = "Accounts payable" }
            });
    }

    public async Task ApplyPaidExpenseAsync(Expense expense)
    {
        if (expense.Status != ExpenseStatus.Paid)
        {
            return;
        }

        var fromBank = expense.PaidFromType.Equals("Bank", StringComparison.OrdinalIgnoreCase);

        if (!fromBank && expense.CashBoxId.HasValue)
        {
            var cashBox = await _context.CashBoxes.FindAsync(expense.CashBoxId.Value);
            if (cashBox is not null)
            {
                cashBox.CurrentBalance -= expense.TotalAmount;
                cashBox.UpdatedAt = DateTime.UtcNow;
            }
        }

        if (fromBank && expense.BankAccountId.HasValue)
        {
            var bank = await _context.BankAccounts.FindAsync(expense.BankAccountId.Value);
            if (bank is not null)
            {
                bank.CurrentBalance -= expense.TotalAmount;
                bank.UpdatedAt = DateTime.UtcNow;
            }
        }

        var expenseAccount = await _chart.ResolveAsync(AccountCodes.MiscExpense);
        var cash = await _chart.ResolveAsync(fromBank ? AccountCodes.Bank : AccountCodes.Cash);

        await BuildAndAddEntryAsync(
            entryDate: expense.ExpenseDate,
            sourceModule: nameof(Expense),
            description: $"Expense {expense.ExpenseNumber}",
            lines: new[]
            {
                new JournalEntryLine { AccountId = expenseAccount.Id, Debit = expense.TotalAmount, Credit = 0, Description = expense.Category },
                new JournalEntryLine { AccountId = cash.Id, Debit = 0, Credit = expense.TotalAmount, Description = expense.PaidFromType }
            });
    }

    public async Task<string> GenerateEntryNumberAsync()
    {
        var next = await _context.JournalEntries.CountAsync() + 1;
        return $"JE-{next:00000}";
    }
}

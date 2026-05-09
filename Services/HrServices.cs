using bestgen.Data;
using bestgen.Models;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services;

/// <summary>
/// HR documents posted to the ledger. The flows assume immediate cash settlement
/// when the document reaches its terminal status (Paid / Approved / Confirmed) —
/// approval-without-payment workflows can be added later by routing the credit
/// side to Salaries Payable (2200) instead of Cash.
/// </summary>
public class PayrollService
{
    private readonly ApplicationDbContext _context;
    private readonly AccountingService _accounting;
    private readonly ChartOfAccounts _chart;

    public PayrollService(ApplicationDbContext context, AccountingService accounting, ChartOfAccounts chart)
    {
        _context = context;
        _accounting = accounting;
        _chart = chart;
    }

    public async Task PrepareAsync(PayrollEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.PayrollNumber))
        {
            var next = await _context.PayrollEntries.CountAsync() + 1;
            entry.PayrollNumber = $"PR-{entry.Year:0000}{entry.Month:00}-{next:00000}";
        }

        // Net = basic + allowances - deductions, defensive in case the form did not.
        entry.NetSalary = entry.BasicSalary + entry.Allowances - entry.Deductions;
    }

    public async Task PostAsync(PayrollEntry entry)
    {
        if (entry.Status == PayrollStatus.Draft || entry.NetSalary <= 0)
        {
            return;
        }

        var salaryExp = await _chart.ResolveAsync(AccountCodes.SalaryExpense);
        var creditAccount = entry.Status == PayrollStatus.Paid
            ? await _chart.ResolveAsync(AccountCodes.Cash)
            : await _chart.ResolveAsync(AccountCodes.SalariesPayable);

        var date = new DateTime(entry.Year, entry.Month, 1);

        await _accounting.BuildAndAddEntryAsync(
            entryDate: date,
            sourceModule: nameof(PayrollEntry),
            description: $"Payroll {entry.PayrollNumber}",
            lines: new[]
            {
                new JournalEntryLine { AccountId = salaryExp.Id, Debit = entry.NetSalary, Credit = 0, Description = "Salary expense" },
                new JournalEntryLine { AccountId = creditAccount.Id, Debit = 0, Credit = entry.NetSalary, Description = entry.Status == PayrollStatus.Paid ? "Cash payment" : "Salaries payable" }
            });
    }
}

public class EmployeeBonusService
{
    private readonly AccountingService _accounting;
    private readonly ChartOfAccounts _chart;

    public EmployeeBonusService(AccountingService accounting, ChartOfAccounts chart)
    {
        _accounting = accounting;
        _chart = chart;
    }

    public async Task PostAsync(EmployeeBonus bonus)
    {
        if (bonus.Status == PayrollStatus.Draft || bonus.Amount <= 0)
        {
            return;
        }

        var bonusExp = await _chart.ResolveAsync(AccountCodes.BonusExpense);
        var credit = bonus.Status == PayrollStatus.Paid
            ? await _chart.ResolveAsync(AccountCodes.Cash)
            : await _chart.ResolveAsync(AccountCodes.SalariesPayable);

        await _accounting.BuildAndAddEntryAsync(
            entryDate: bonus.Date,
            sourceModule: nameof(EmployeeBonus),
            description: $"Employee bonus {bonus.Reason ?? string.Empty}".Trim(),
            lines: new[]
            {
                new JournalEntryLine { AccountId = bonusExp.Id, Debit = bonus.Amount, Credit = 0, Description = "Bonus expense" },
                new JournalEntryLine { AccountId = credit.Id, Debit = 0, Credit = bonus.Amount, Description = bonus.Status == PayrollStatus.Paid ? "Cash payment" : "Salaries payable" }
            });
    }
}

public class EmployeeDeductionService
{
    private readonly AccountingService _accounting;
    private readonly ChartOfAccounts _chart;

    public EmployeeDeductionService(AccountingService accounting, ChartOfAccounts chart)
    {
        _accounting = accounting;
        _chart = chart;
    }

    public async Task PostAsync(EmployeeDeduction deduction)
    {
        if (deduction.Status == PayrollStatus.Draft || deduction.Amount <= 0)
        {
            return;
        }

        // A deduction reduces the employee's net pay: DR Salaries Payable (we owe less),
        // CR Salary Expense (effectively reverses part of payroll).
        var salariesPayable = await _chart.ResolveAsync(AccountCodes.SalariesPayable);
        var salaryExp = await _chart.ResolveAsync(AccountCodes.SalaryExpense);

        await _accounting.BuildAndAddEntryAsync(
            entryDate: deduction.Date,
            sourceModule: nameof(EmployeeDeduction),
            description: $"Employee deduction {deduction.Reason ?? string.Empty}".Trim(),
            lines: new[]
            {
                new JournalEntryLine { AccountId = salariesPayable.Id, Debit = deduction.Amount, Credit = 0, Description = "Salaries payable" },
                new JournalEntryLine { AccountId = salaryExp.Id, Debit = 0, Credit = deduction.Amount, Description = "Salary expense reversal" }
            });
    }
}

public class EmployeeLoanService
{
    private readonly AccountingService _accounting;
    private readonly ChartOfAccounts _chart;

    public EmployeeLoanService(AccountingService accounting, ChartOfAccounts chart)
    {
        _accounting = accounting;
        _chart = chart;
    }

    public async Task PostAsync(EmployeeLoan loan)
    {
        if (loan.Status != LoanStatus.Active || loan.LoanAmount <= 0)
        {
            return;
        }

        // Loan disbursement: DR AR-Employees (1110), CR Cash (1000).
        var arEmployees = await _chart.ResolveAsync(AccountCodes.AccountsReceivableEmployees);
        var cash = await _chart.ResolveAsync(AccountCodes.Cash);

        await _accounting.BuildAndAddEntryAsync(
            entryDate: loan.LoanDate,
            sourceModule: nameof(EmployeeLoan),
            description: $"Employee loan disbursement",
            lines: new[]
            {
                new JournalEntryLine { AccountId = arEmployees.Id, Debit = loan.LoanAmount, Credit = 0, Description = "Employee loan" },
                new JournalEntryLine { AccountId = cash.Id, Debit = 0, Credit = loan.LoanAmount, Description = "Cash disbursed" }
            });
    }
}

public class EmployeeReceiptService
{
    private readonly ApplicationDbContext _context;
    private readonly AccountingService _accounting;
    private readonly ChartOfAccounts _chart;

    public EmployeeReceiptService(ApplicationDbContext context, AccountingService accounting, ChartOfAccounts chart)
    {
        _context = context;
        _accounting = accounting;
        _chart = chart;
    }

    public async Task PrepareAsync(EmployeeReceipt receipt)
    {
        if (string.IsNullOrWhiteSpace(receipt.ReceiptNumber))
        {
            var next = await _context.EmployeeReceipts.CountAsync() + 1;
            receipt.ReceiptNumber = $"ER-{DateTime.Today:yyyy}-{next:00000}";
        }
    }

    public async Task PostAsync(EmployeeReceipt receipt)
    {
        if (receipt.Status != ReceiptStatus.Confirmed || receipt.Amount <= 0)
        {
            return;
        }

        // Pay-out to the employee against Salaries Payable.
        var salariesPayable = await _chart.ResolveAsync(AccountCodes.SalariesPayable);
        var cash = await _chart.ResolveAsync(AccountCodes.Cash);

        await _accounting.BuildAndAddEntryAsync(
            entryDate: receipt.Date,
            sourceModule: nameof(EmployeeReceipt),
            description: $"Employee receipt {receipt.ReceiptNumber}",
            lines: new[]
            {
                new JournalEntryLine { AccountId = salariesPayable.Id, Debit = receipt.Amount, Credit = 0, Description = "Salaries payable" },
                new JournalEntryLine { AccountId = cash.Id, Debit = 0, Credit = receipt.Amount, Description = "Cash to employee" }
            });
    }
}

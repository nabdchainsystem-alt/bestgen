using bestgen.Data;
using bestgen.Models;
using bestgen.Services;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Controllers;

public class ExpensesController : CrudController<Expense>
{
    private readonly AccountingService _accountingService;

    public ExpensesController(ApplicationDbContext context, AccountingService accountingService) : base(context)
    {
        _accountingService = accountingService;
    }

    protected override IQueryable<Expense> Query() => Context.Expenses
        .AsNoTracking()
        .Include(expense => expense.CashBox)
        .Include(expense => expense.BankAccount);

    protected override async Task BeforeCreateSaveAsync(Expense entity)
    {
        if (entity.TotalAmount == 0)
        {
            entity.TotalAmount = entity.AmountBeforeVat + entity.VatAmount;
        }

        await _accountingService.ApplyPaidExpenseAsync(entity);
    }
}

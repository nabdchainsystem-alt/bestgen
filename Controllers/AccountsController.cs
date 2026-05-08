using bestgen.Data;
using bestgen.Models;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Controllers;

public class AccountsController : CrudController<Account>
{
    public AccountsController(ApplicationDbContext context) : base(context)
    {
    }

    protected override IQueryable<Account> Query() => Context.Accounts
        .AsNoTracking()
        .Include(account => account.ParentAccount);
}

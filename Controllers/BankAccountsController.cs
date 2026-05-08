using bestgen.Data;
using bestgen.Models;

namespace bestgen.Controllers;

public class BankAccountsController : CrudController<BankAccount>
{
    public BankAccountsController(ApplicationDbContext context) : base(context)
    {
    }
}

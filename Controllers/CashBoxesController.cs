using bestgen.Data;
using bestgen.Models;

namespace bestgen.Controllers;

public class CashBoxesController : CrudController<CashBox>
{
    public CashBoxesController(ApplicationDbContext context) : base(context)
    {
    }
}

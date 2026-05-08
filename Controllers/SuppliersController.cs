using bestgen.Data;
using bestgen.Models;

namespace bestgen.Controllers;

public class SuppliersController : CrudController<Supplier>
{
    public SuppliersController(ApplicationDbContext context) : base(context)
    {
    }
}

using bestgen.Data;
using bestgen.Models;

namespace bestgen.Controllers;

public class WarehousesController : CrudController<Warehouse>
{
    public WarehousesController(ApplicationDbContext context) : base(context)
    {
    }
}

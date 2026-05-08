using bestgen.Data;
using bestgen.Models;

namespace bestgen.Controllers;

public class CustomersController : CrudController<Customer>
{
    public CustomersController(ApplicationDbContext context) : base(context)
    {
    }
}

using bestgen.Data;
using bestgen.Models;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Controllers;

public class ProductsController : CrudController<Product>
{
    public ProductsController(ApplicationDbContext context) : base(context)
    {
    }

    protected override IQueryable<Product> Query() => Context.Products
        .AsNoTracking()
        .Include(product => product.Warehouse);
}

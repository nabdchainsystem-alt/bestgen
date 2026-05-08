using bestgen.Data;
using bestgen.Models;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services;

public class InventoryService
{
    private readonly ApplicationDbContext _context;

    public InventoryService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task ReduceStockAsync(IEnumerable<SalesInvoiceItem> items)
    {
        foreach (var item in items)
        {
            var product = await _context.Products.FirstOrDefaultAsync(x => x.Id == item.ProductId);
            if (product is null || !product.TrackInventory)
            {
                continue;
            }

            product.CurrentStock -= item.Quantity;
            product.UpdatedAt = DateTime.UtcNow;
        }
    }

    public async Task IncreaseStockAsync(IEnumerable<PurchaseInvoiceItem> items)
    {
        foreach (var item in items)
        {
            var product = await _context.Products.FirstOrDefaultAsync(x => x.Id == item.ProductId);
            if (product is null || !product.TrackInventory)
            {
                continue;
            }

            product.CurrentStock += item.Quantity;
            product.PurchasePrice = item.UnitCost;
            product.UpdatedAt = DateTime.UtcNow;
        }
    }

    public async Task<List<Product>> GetLowStockProductsAsync(int take = 10)
    {
        // SQLite cannot ORDER BY decimal — materialize then sort client-side.
        var matches = await _context.Products
            .AsNoTracking()
            .Where(product => product.TrackInventory && product.CurrentStock <= product.MinimumStockLevel)
            .ToListAsync();

        return matches
            .OrderBy(product => product.CurrentStock)
            .Take(take)
            .ToList();
    }
}

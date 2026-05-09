using bestgen.Data;
using bestgen.Models;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services;

/// <summary>
/// High-level inventory operations used by the orchestration services. The actual
/// stock-level mutation + audit-trail writing lives in <see cref="StockMovementService"/>;
/// this layer just exposes invoice-shaped helpers and inventory queries.
/// </summary>
public class InventoryService
{
    private readonly ApplicationDbContext _context;
    private readonly StockMovementService _movements;

    public InventoryService(ApplicationDbContext context, StockMovementService movements)
    {
        _context = context;
        _movements = movements;
    }

    public Task ReduceStockAsync(SalesInvoice invoice) =>
        _movements.RecordSalesIssueAsync(invoice);

    public Task IncreaseStockAsync(PurchaseInvoice invoice) =>
        _movements.RecordPurchaseReceiptAsync(invoice);

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

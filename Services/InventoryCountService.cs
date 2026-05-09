using bestgen.Data;
using bestgen.Models;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services;

/// <summary>
/// Reconciles physical stock against system stock for a warehouse. When the
/// count is approved, every line whose CountedQuantity differs from
/// SystemQuantity creates an adjustment <see cref="StockMovement"/> and the
/// total variance posts a single journal entry against Inventory ↔ Inventory
/// Variance (5800).
/// </summary>
public class InventoryCountService
{
    private readonly ApplicationDbContext _context;
    private readonly StockMovementService _movements;
    private readonly AccountingService _accounting;
    private readonly ChartOfAccounts _chart;

    public InventoryCountService(
        ApplicationDbContext context,
        StockMovementService movements,
        AccountingService accounting,
        ChartOfAccounts chart)
    {
        _context = context;
        _movements = movements;
        _accounting = accounting;
        _chart = chart;
    }

    public async Task PrepareAsync(InventoryCount count)
    {
        if (string.IsNullOrWhiteSpace(count.InventoryCountNumber))
        {
            count.InventoryCountNumber = await GenerateNumberAsync();
        }

        // Compute Difference for each item — defensive in case the form did not.
        foreach (var item in count.Items)
        {
            item.Difference = item.CountedQuantity - item.SystemQuantity;
        }
    }

    public async Task ApplyAsync(InventoryCount count)
    {
        if (count.Status != InventoryCountStatus.Approved || count.Items.Count == 0)
        {
            return;
        }

        var reference = $"InventoryCount:{count.Id} ({count.InventoryCountNumber})";
        decimal totalVariance = 0m;

        foreach (var item in count.Items.Where(i => i.Difference != 0))
        {
            var product = await _context.Products.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == item.ProductId);
            if (product is null) continue;

            await _movements.RecordCountAdjustmentAsync(
                productId: item.ProductId,
                warehouseId: count.WarehouseId,
                deltaQuantity: item.Difference,
                unitCost: product.PurchasePrice,
                reference: reference,
                movementDate: count.Date);

            totalVariance += item.Difference * product.PurchasePrice;
        }

        if (totalVariance == 0m)
        {
            return;
        }

        var inventory = await _chart.ResolveAsync(AccountCodes.Inventory);
        var variance = await _chart.ResolveAsync(AccountCodes.InventoryVariance);

        var lines = totalVariance > 0
            ? new[]
            {
                // Surplus: inventory increases, variance recognised as a credit (gain).
                new JournalEntryLine { AccountId = inventory.Id, Debit = totalVariance, Credit = 0, Description = "Inventory adjustment (gain)" },
                new JournalEntryLine { AccountId = variance.Id, Debit = 0, Credit = totalVariance, Description = "Inventory variance" }
            }
            : new[]
            {
                // Shortage: variance becomes an expense, inventory decreases.
                new JournalEntryLine { AccountId = variance.Id, Debit = -totalVariance, Credit = 0, Description = "Inventory variance" },
                new JournalEntryLine { AccountId = inventory.Id, Debit = 0, Credit = -totalVariance, Description = "Inventory adjustment (shortage)" }
            };

        await _accounting.BuildAndAddEntryAsync(
            entryDate: count.Date,
            sourceModule: nameof(InventoryCount),
            description: $"Inventory count {count.InventoryCountNumber}",
            lines: lines);
    }

    private async Task<string> GenerateNumberAsync()
    {
        var next = await _context.InventoryCounts.CountAsync() + 1;
        return $"IC-{DateTime.Today:yyyy}-{next:00000}";
    }
}

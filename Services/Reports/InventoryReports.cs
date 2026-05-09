using System.Globalization;
using bestgen.Data;
using bestgen.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services.Reports;

/// <summary>
/// Inventory snapshots and movement history.
/// </summary>
public class InventoryReports
{
    private readonly ApplicationDbContext _context;

    public InventoryReports(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ReportResult> CurrentStockAsync(ReportFilters filters)
    {
        var query = _context.Products.AsNoTracking()
            .Include(p => p.Warehouse)
            .Include(p => p.ProductCategory)
            .Where(p => p.TrackInventory);

        if (filters.WarehouseId is int wid) query = query.Where(p => p.WarehouseId == wid);

        var products = await query.OrderBy(p => p.NameAr).ToListAsync();

        var rows = products.Select(p => new ReportRow(new Dictionary<string, string?>
        {
            ["sku"] = p.SKU,
            ["name"] = p.NameAr,
            ["category"] = p.ProductCategory?.NameAr ?? p.Category ?? "-",
            ["warehouse"] = p.Warehouse?.Name ?? "-",
            ["stock"] = Money(p.CurrentStock),
            ["min"] = Money(p.MinimumStockLevel),
            ["cost"] = Money(p.PurchasePrice),
            ["value"] = Money(p.CurrentStock * p.PurchasePrice)
        })).ToList();

        return new ReportResult(
            ReportKey: "current-stock",
            TitleEn: "Current Stock",
            TitleAr: "المخزون الحالي",
            Columns: new[]
            {
                new ReportColumn("sku", "SKU", "SKU"),
                new ReportColumn("name", "Product", "المنتج"),
                new ReportColumn("category", "Category", "التصنيف"),
                new ReportColumn("warehouse", "Warehouse", "المستودع"),
                new ReportColumn("stock", "Quantity", "الكمية", "end", true),
                new ReportColumn("min", "Min", "الحد الأدنى", "end", true),
                new ReportColumn("cost", "Unit cost", "تكلفة الوحدة", "end", true),
                new ReportColumn("value", "Value", "القيمة", "end", true)
            },
            Rows: rows,
            Totals: new[]
            {
                new ReportTotal("Inventory value", "قيمة المخزون",
                    Money(products.Sum(p => p.CurrentStock * p.PurchasePrice)), IsMoney: true)
            },
            Filters: filters);
    }

    public async Task<ReportResult> StockMovementLedgerAsync(ReportFilters filters)
    {
        var (from, to) = ResolvePeriod(filters);

        var query = _context.StockMovements.AsNoTracking()
            .Include(m => m.Product)
            .Include(m => m.Warehouse)
            .Where(m => m.MovementDate >= from && m.MovementDate <= to);

        if (filters.ProductId is int pid) query = query.Where(m => m.ProductId == pid);
        if (filters.WarehouseId is int wid) query = query.Where(m => m.WarehouseId == wid);

        var movements = await query
            .OrderBy(m => m.ProductId)
            .ThenBy(m => m.MovementDate)
            .ToListAsync();

        var rows = movements.Select(m => new ReportRow(new Dictionary<string, string?>
        {
            ["date"] = m.MovementDate.ToString("yyyy-MM-dd"),
            ["product"] = m.Product?.NameAr ?? "-",
            ["warehouse"] = m.Warehouse?.Name ?? "-",
            ["type"] = m.MovementType.ToString(),
            ["qty"] = Money(m.Quantity),
            ["cost"] = m.UnitCost.HasValue ? Money(m.UnitCost.Value) : "-",
            ["reference"] = m.Reference ?? "-"
        })).ToList();

        return new ReportResult(
            ReportKey: "inventory-ledger",
            TitleEn: "Stock Movement Ledger",
            TitleAr: "أستاذ المخزون",
            Columns: new[]
            {
                new ReportColumn("date", "Date", "التاريخ"),
                new ReportColumn("product", "Product", "المنتج"),
                new ReportColumn("warehouse", "Warehouse", "المستودع"),
                new ReportColumn("type", "Type", "النوع"),
                new ReportColumn("qty", "Quantity", "الكمية", "end", true),
                new ReportColumn("cost", "Unit cost", "تكلفة الوحدة", "end", true),
                new ReportColumn("reference", "Reference", "المرجع")
            },
            Rows: rows,
            Totals: Array.Empty<ReportTotal>(),
            Filters: filters);
    }

    private static (DateTime from, DateTime to) ResolvePeriod(ReportFilters filters)
    {
        var to = filters.ToDate ?? DateTime.Today;
        var from = filters.FromDate ?? new DateTime(to.Year, to.Month, 1);
        if (from > to) (from, to) = (to, from);
        return (from, to);
    }

    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
}

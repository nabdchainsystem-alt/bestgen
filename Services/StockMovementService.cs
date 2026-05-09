using bestgen.Data;
using bestgen.Models;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services;

/// <summary>
/// Centralized stock-mutation entry point. Every change to <see cref="Product.CurrentStock"/>
/// must go through this service — it writes a paired <see cref="StockMovement"/> row so the
/// inventory ledger stays auditable.
///
/// Sign convention: <c>signedQuantity</c> is positive for inbound (receipts, returns, opening,
/// transfer destination) and negative for outbound (sales issue, transfer source, write-off).
///
/// Like the other transactional services this does NOT call SaveChanges; the orchestrating
/// service is responsible for committing the unit of work.
/// </summary>
public class StockMovementService
{
    private readonly ApplicationDbContext _context;

    public StockMovementService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<StockMovement?> RecordAsync(
        int productId,
        int? warehouseId,
        StockMovementType type,
        decimal signedQuantity,
        decimal? unitCost,
        string? reference,
        DateTime? movementDate = null,
        string? notes = null)
    {
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId);
        if (product is null || !product.TrackInventory)
        {
            return null;
        }

        product.CurrentStock += signedQuantity;
        product.UpdatedAt = DateTime.UtcNow;

        var movement = new StockMovement
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            MovementType = type,
            Quantity = signedQuantity,
            UnitCost = unitCost,
            Reference = reference,
            Notes = notes,
            MovementDate = movementDate ?? DateTime.UtcNow
        };

        _context.StockMovements.Add(movement);
        return movement;
    }

    public async Task RecordSalesIssueAsync(SalesInvoice invoice)
    {
        var reference = BuildReference(nameof(SalesInvoice), invoice.Id, invoice.InvoiceNumber);
        foreach (var item in invoice.Items)
        {
            await RecordAsync(
                productId: item.ProductId,
                warehouseId: invoice.WarehouseId,
                type: StockMovementType.Sales,
                signedQuantity: -item.Quantity,
                unitCost: item.UnitPrice,
                reference: reference,
                movementDate: invoice.InvoiceDate);
        }
    }

    public async Task RecordPurchaseReceiptAsync(PurchaseInvoice invoice)
    {
        var reference = BuildReference(nameof(PurchaseInvoice), invoice.Id, invoice.PurchaseInvoiceNumber);
        foreach (var item in invoice.Items)
        {
            await RecordAsync(
                productId: item.ProductId,
                warehouseId: invoice.WarehouseId,
                type: StockMovementType.Purchase,
                signedQuantity: item.Quantity,
                unitCost: item.UnitCost,
                reference: reference,
                movementDate: invoice.InvoiceDate);

            // Track latest purchase cost on the product so reporting and replenishment
            // logic stays in sync. Mirrors the legacy InventoryService behavior.
            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId);
            if (product is not null && product.TrackInventory)
            {
                product.PurchasePrice = item.UnitCost;
            }
        }
    }

    public async Task RecordTransferAsync(StockTransfer transfer, IEnumerable<StockTransferItem> items)
    {
        var reference = BuildReference(nameof(StockTransfer), transfer.Id, transfer.TransferNumber);
        foreach (var item in items)
        {
            await RecordAsync(
                productId: item.ProductId,
                warehouseId: transfer.FromWarehouseId,
                type: StockMovementType.Transfer,
                signedQuantity: -item.Quantity,
                unitCost: null,
                reference: reference,
                movementDate: transfer.Date);

            await RecordAsync(
                productId: item.ProductId,
                warehouseId: transfer.ToWarehouseId,
                type: StockMovementType.Transfer,
                signedQuantity: item.Quantity,
                unitCost: null,
                reference: reference,
                movementDate: transfer.Date);
        }
    }

    public Task RecordCountAdjustmentAsync(
        int productId,
        int warehouseId,
        decimal deltaQuantity,
        decimal? unitCost,
        string reference,
        DateTime movementDate) =>
        RecordAsync(
            productId,
            warehouseId,
            StockMovementType.Adjustment,
            deltaQuantity,
            unitCost,
            reference,
            movementDate);

    public Task RecordOpeningAsync(
        int productId,
        int warehouseId,
        decimal quantity,
        decimal unitCost,
        DateTime movementDate) =>
        RecordAsync(
            productId,
            warehouseId,
            StockMovementType.Opening,
            quantity,
            unitCost,
            reference: "OpeningBalance",
            movementDate);

    public Task RecordReturnInboundAsync(
        int productId,
        int? warehouseId,
        decimal quantity,
        decimal? unitCost,
        string reference,
        DateTime movementDate) =>
        RecordAsync(productId, warehouseId, StockMovementType.Return, quantity, unitCost, reference, movementDate);

    public Task RecordReturnOutboundAsync(
        int productId,
        int? warehouseId,
        decimal quantity,
        decimal? unitCost,
        string reference,
        DateTime movementDate) =>
        RecordAsync(productId, warehouseId, StockMovementType.Return, -quantity, unitCost, reference, movementDate);

    private static string BuildReference(string entityName, int id, string? humanNumber) =>
        string.IsNullOrWhiteSpace(humanNumber)
            ? $"{entityName}:{id}"
            : $"{entityName}:{id} ({humanNumber})";
}

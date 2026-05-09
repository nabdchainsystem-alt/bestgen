using bestgen.Data;
using bestgen.Models;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services;

/// <summary>
/// Moves stock between two warehouses. When the transfer reaches the
/// <see cref="StockTransferStatus.Transferred"/> state we write paired
/// <see cref="StockMovement"/> rows (negative on the source warehouse,
/// positive on the destination) via <see cref="StockMovementService"/>.
/// No journal entry is posted because both warehouses share the same
/// Inventory account in the chart.
/// </summary>
public class StockTransferService
{
    private readonly ApplicationDbContext _context;
    private readonly StockMovementService _movements;

    public StockTransferService(ApplicationDbContext context, StockMovementService movements)
    {
        _context = context;
        _movements = movements;
    }

    public async Task PrepareAsync(StockTransfer transfer)
    {
        if (string.IsNullOrWhiteSpace(transfer.TransferNumber))
        {
            transfer.TransferNumber = await GenerateNumberAsync();
        }
    }

    public async Task ApplyMovementsAsync(StockTransfer transfer)
    {
        if (transfer.Status != StockTransferStatus.Transferred)
        {
            return;
        }

        // The transfer's Items are loaded by the caller (or by the auto-CRUD path
        // they may be empty until items are managed via a richer multi-line form).
        if (transfer.Items.Count == 0)
        {
            return;
        }

        await _movements.RecordTransferAsync(transfer, transfer.Items);
    }

    private async Task<string> GenerateNumberAsync()
    {
        var next = await _context.StockTransfers.CountAsync() + 1;
        return $"ST-{DateTime.Today:yyyy}-{next:00000}";
    }
}

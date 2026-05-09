using bestgen.Data;
using bestgen.Models;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services;

/// <summary>
/// Records that goods were physically received from a supplier. Today the
/// PurchaseInvoice flow already moves stock + posts the AP entry, so the
/// GoodsReceipt sits as a paperwork/audit record alongside it. If the system
/// later moves to a three-way-match model (PO → GR → Invoice with GR/IR
/// clearing), expand this service to post DR Inventory, CR GR/IR (2230) and
/// have the matching purchase invoice clear that account instead of crediting AP.
/// </summary>
public class GoodsReceiptService
{
    private readonly ApplicationDbContext _context;

    public GoodsReceiptService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task PrepareAsync(GoodsReceipt receipt)
    {
        if (string.IsNullOrWhiteSpace(receipt.GoodsReceiptNumber))
        {
            receipt.GoodsReceiptNumber = await GenerateNumberAsync();
        }
    }

    private async Task<string> GenerateNumberAsync()
    {
        var next = await _context.GoodsReceipts.CountAsync() + 1;
        return $"GR-{DateTime.Today:yyyy}-{next:00000}";
    }
}

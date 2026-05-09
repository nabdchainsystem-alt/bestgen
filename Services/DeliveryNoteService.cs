using bestgen.Data;
using bestgen.Models;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services;

/// <summary>
/// Documentary note that the goods on a related sales invoice were physically delivered.
/// Stock movement (and any COGS posting) for a sale lives on the SalesInvoice flow today,
/// so the delivery note itself doesn't reduce stock — it stays as a paperwork record
/// linked to the sales invoice and customer. If a delivery note ever needs to issue
/// goods independently (loose delivery, no invoice), expand this service.
///
/// We still generate the document number and stamp the metadata to keep things tidy.
/// </summary>
public class DeliveryNoteService
{
    private readonly ApplicationDbContext _context;

    public DeliveryNoteService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task PrepareAsync(DeliveryNote note)
    {
        if (string.IsNullOrWhiteSpace(note.DeliveryNoteNumber))
        {
            note.DeliveryNoteNumber = await GenerateNumberAsync();
        }
    }

    private async Task<string> GenerateNumberAsync()
    {
        var next = await _context.DeliveryNotes.CountAsync() + 1;
        return $"DN-{DateTime.Today:yyyy}-{next:00000}";
    }
}

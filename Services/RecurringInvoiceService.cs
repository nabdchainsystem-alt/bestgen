using bestgen.Data;
using bestgen.Models;
using bestgen.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services;

/// <summary>
/// Walks active recurring-invoice templates whose NextRunDate is due and
/// generates a real SalesInvoice via <see cref="SalesInvoiceService"/>, then
/// advances NextRunDate by the template's frequency.
/// </summary>
public class RecurringInvoiceService
{
    private readonly ApplicationDbContext _db;
    private readonly SalesInvoiceService _sales;

    public RecurringInvoiceService(ApplicationDbContext db, SalesInvoiceService sales)
    {
        _db = db;
        _sales = sales;
    }

    public static DateTime ComputeNextRun(DateTime current, RecurrenceFrequency f) => f switch
    {
        RecurrenceFrequency.Daily     => current.AddDays(1),
        RecurrenceFrequency.Weekly    => current.AddDays(7),
        RecurrenceFrequency.Monthly   => current.AddMonths(1),
        RecurrenceFrequency.Quarterly => current.AddMonths(3),
        RecurrenceFrequency.Yearly    => current.AddYears(1),
        _ => current.AddMonths(1)
    };

    public async Task<int> RunDueAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        var due = await _db.RecurringInvoices
            .Where(r => r.IsActive
                     && r.NextRunDate.Date <= today
                     && (r.EndDate == null || r.EndDate >= today))
            .ToListAsync(ct);

        var generated = 0;
        foreach (var template in due)
        {
            var invoice = await GenerateOnceAsync(template, ct);
            if (invoice is not null) generated++;
        }
        return generated;
    }

    public async Task<SalesInvoice?> GenerateOneByIdAsync(int recurringId, CancellationToken ct = default)
    {
        var template = await _db.RecurringInvoices.FirstOrDefaultAsync(r => r.Id == recurringId, ct);
        if (template is null) return null;
        return await GenerateOnceAsync(template, ct);
    }

    private async Task<SalesInvoice?> GenerateOnceAsync(RecurringInvoice template, CancellationToken ct)
    {
        // Build a one-line form model from the template.
        var vm = new SalesInvoiceFormViewModel
        {
            InvoiceDate = DateTime.UtcNow.Date,
            CustomerId = template.CustomerId,
            WarehouseId = template.WarehouseId,
            PaymentMethod = PaymentMethod.Credit,
            Status = InvoiceStatus.Issued,
            Notes = $"Auto-generated from recurring template: {template.Name}"
                   + (string.IsNullOrWhiteSpace(template.Notes) ? "" : "\n" + template.Notes),
            Items = new List<SalesInvoiceItemFormViewModel>
            {
                new()
                {
                    ProductId = template.ProductId,
                    Quantity = template.Quantity,
                    UnitPrice = template.UnitPrice,
                    Discount = template.Discount,
                    VatRate = template.VatRate
                }
            }
        };

        var invoice = await _sales.CreateAsync(vm);

        template.LastGeneratedAt = DateTime.UtcNow;
        template.LastGeneratedInvoiceId = invoice.Id;
        template.NextRunDate = ComputeNextRun(template.NextRunDate, template.Frequency);
        await _db.SaveChangesAsync(ct);
        return invoice;
    }
}

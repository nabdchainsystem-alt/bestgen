using bestgen.Data;
using bestgen.Models;
using bestgen.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services;

public class SalesQuotationService
{
    private readonly ApplicationDbContext _context;
    private readonly InvoiceCalculationService _calc;

    public SalesQuotationService(ApplicationDbContext context, InvoiceCalculationService calc)
    {
        _context = context;
        _calc = calc;
    }

    public async Task<SalesQuotation> CreateAsync(SalesQuotationFormViewModel model)
    {
        _calc.CalculateQuotation(model);

        var quotation = new SalesQuotation
        {
            QuotationNumber = string.IsNullOrWhiteSpace(model.QuotationNumber)
                ? await GenerateNumberAsync()
                : model.QuotationNumber,
            QuotationDate = model.QuotationDate,
            ValidUntil = model.ValidUntil,
            CustomerId = model.CustomerId,
            Subtotal = model.Subtotal,
            DiscountTotal = model.DiscountTotal,
            VatTotal = model.VatTotal,
            GrandTotal = model.GrandTotal,
            Status = model.Status,
            Notes = model.Notes,
            Terms = model.Terms,
            Items = model.Items.Select(item => new SalesQuotationItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Discount = item.Discount,
                VatRate = item.VatRate,
                LineTotal = item.LineTotal
            }).ToList()
        };

        _context.SalesQuotations.Add(quotation);
        await _context.SaveChangesAsync();
        bestgen.Services.Observability.BestgenMetrics.QuotationsCreated.Inc();
        return quotation;
    }

    public async Task UpdateAsync(int id, SalesQuotationFormViewModel model)
    {
        _calc.CalculateQuotation(model);

        var quotation = await _context.SalesQuotations
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new InvalidOperationException("Quotation not found.");

        quotation.QuotationDate = model.QuotationDate;
        quotation.ValidUntil = model.ValidUntil;
        quotation.CustomerId = model.CustomerId;
        quotation.Subtotal = model.Subtotal;
        quotation.DiscountTotal = model.DiscountTotal;
        quotation.VatTotal = model.VatTotal;
        quotation.GrandTotal = model.GrandTotal;
        quotation.Status = model.Status;
        quotation.Notes = model.Notes;
        quotation.Terms = model.Terms;
        quotation.UpdatedAt = DateTime.UtcNow;

        _context.SalesQuotationItems.RemoveRange(quotation.Items);
        var newItems = model.Items.Select(item => new SalesQuotationItem
        {
            SalesQuotationId = quotation.Id,
            ProductId = item.ProductId,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice,
            Discount = item.Discount,
            VatRate = item.VatRate,
            LineTotal = item.LineTotal
        }).ToList();
        await _context.SalesQuotationItems.AddRangeAsync(newItems);
        await _context.SaveChangesAsync();
    }

    public SalesQuotationFormViewModel ToFormViewModel(SalesQuotation quotation) => new()
    {
        Id = quotation.Id,
        QuotationNumber = quotation.QuotationNumber,
        QuotationDate = quotation.QuotationDate,
        ValidUntil = quotation.ValidUntil,
        CustomerId = quotation.CustomerId,
        Subtotal = quotation.Subtotal,
        DiscountTotal = quotation.DiscountTotal,
        VatTotal = quotation.VatTotal,
        GrandTotal = quotation.GrandTotal,
        Status = quotation.Status,
        Notes = quotation.Notes,
        Terms = quotation.Terms,
        Items = quotation.Items.Select(item => new SalesQuotationItemFormViewModel
        {
            ProductId = item.ProductId,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice,
            Discount = item.Discount,
            VatRate = item.VatRate,
            LineTotal = item.LineTotal
        }).ToList()
    };

    private async Task<string> GenerateNumberAsync()
    {
        var next = await _context.SalesQuotations.CountAsync() + 1;
        return $"QUO-{DateTime.Today:yyyy}-{next:00000}";
    }
}

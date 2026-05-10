using bestgen.Data;
using bestgen.Models;
using bestgen.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services;

public class SalesInvoiceService
{
    private readonly ApplicationDbContext _context;
    private readonly InvoiceCalculationService _calculationService;
    private readonly InventoryService _inventoryService;
    private readonly AccountingService _accountingService;
    private readonly PartyBalanceService _partyBalanceService;

    public SalesInvoiceService(
        ApplicationDbContext context,
        InvoiceCalculationService calculationService,
        InventoryService inventoryService,
        AccountingService accountingService,
        PartyBalanceService partyBalanceService)
    {
        _context = context;
        _calculationService = calculationService;
        _inventoryService = inventoryService;
        _accountingService = accountingService;
        _partyBalanceService = partyBalanceService;
    }

    public async Task<SalesInvoice> CreateAsync(SalesInvoiceFormViewModel model)
    {
        _calculationService.CalculateSales(model);

        var invoice = new SalesInvoice
        {
            InvoiceNumber = string.IsNullOrWhiteSpace(model.InvoiceNumber)
                ? await GenerateInvoiceNumberAsync()
                : model.InvoiceNumber,
            InvoiceDate = model.InvoiceDate,
            CustomerId = model.CustomerId,
            WarehouseId = model.WarehouseId,
            Subtotal = model.Subtotal,
            DiscountTotal = model.DiscountTotal,
            VatTotal = model.VatTotal,
            GrandTotal = model.GrandTotal,
            PaidAmount = model.PaidAmount,
            RemainingAmount = model.RemainingAmount,
            PaymentMethod = model.PaymentMethod,
            Status = model.Status,
            Notes = model.Notes,
            CurrencyCode = string.IsNullOrWhiteSpace(model.CurrencyCode) ? "SAR" : model.CurrencyCode,
            ExchangeRate = model.ExchangeRate <= 0 ? 1m : model.ExchangeRate,
            Items = model.Items.Select(item => new SalesInvoiceItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Discount = item.Discount,
                VatRate = item.VatRate,
                LineTotal = item.LineTotal
            }).ToList()
        };

        _context.SalesInvoices.Add(invoice);
        if (invoice.Status is InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid or InvoiceStatus.Paid)
        {
            await _inventoryService.ReduceStockAsync(invoice);
        }
        await _accountingService.CreateSalesJournalEntryAsync(invoice);
        await _context.SaveChangesAsync();

        await _partyBalanceService.RecalculateCustomerAsync(invoice.CustomerId);
        await _context.SaveChangesAsync();
        bestgen.Services.Observability.BestgenMetrics.InvoicesCreated.WithLabels("sales").Inc();
        return invoice;
    }

    public async Task UpdateAsync(int id, SalesInvoiceFormViewModel model)
    {
        _calculationService.CalculateSales(model);

        var invoice = await _context.SalesInvoices
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (invoice is null)
        {
            throw new InvalidOperationException("Sales invoice was not found.");
        }

        invoice.InvoiceDate = model.InvoiceDate;
        invoice.CustomerId = model.CustomerId;
        invoice.WarehouseId = model.WarehouseId;
        invoice.Subtotal = model.Subtotal;
        invoice.DiscountTotal = model.DiscountTotal;
        invoice.VatTotal = model.VatTotal;
        invoice.GrandTotal = model.GrandTotal;
        invoice.PaidAmount = model.PaidAmount;
        invoice.RemainingAmount = model.RemainingAmount;
        invoice.PaymentMethod = model.PaymentMethod;
        invoice.Status = model.Status;
        invoice.Notes = model.Notes;
        invoice.CurrencyCode = string.IsNullOrWhiteSpace(model.CurrencyCode) ? "SAR" : model.CurrencyCode;
        invoice.ExchangeRate = model.ExchangeRate <= 0 ? 1m : model.ExchangeRate;
        invoice.UpdatedAt = DateTime.UtcNow;

        _context.SalesInvoiceItems.RemoveRange(invoice.Items);
        var newItems = model.Items.Select(item => new SalesInvoiceItem
        {
            SalesInvoiceId = invoice.Id,
            ProductId = item.ProductId,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice,
            Discount = item.Discount,
            VatRate = item.VatRate,
            LineTotal = item.LineTotal
        }).ToList();
        await _context.SalesInvoiceItems.AddRangeAsync(newItems);

        await _context.SaveChangesAsync();

        await _partyBalanceService.RecalculateCustomerAsync(invoice.CustomerId);
        await _context.SaveChangesAsync();
    }

    public SalesInvoiceFormViewModel ToFormViewModel(SalesInvoice invoice) => new()
    {
        Id = invoice.Id,
        InvoiceNumber = invoice.InvoiceNumber,
        InvoiceDate = invoice.InvoiceDate,
        CustomerId = invoice.CustomerId,
        WarehouseId = invoice.WarehouseId,
        Subtotal = invoice.Subtotal,
        DiscountTotal = invoice.DiscountTotal,
        VatTotal = invoice.VatTotal,
        GrandTotal = invoice.GrandTotal,
        PaidAmount = invoice.PaidAmount,
        RemainingAmount = invoice.RemainingAmount,
        PaymentMethod = invoice.PaymentMethod,
        Status = invoice.Status,
        Notes = invoice.Notes,
        CurrencyCode = invoice.CurrencyCode,
        ExchangeRate = invoice.ExchangeRate,
        Items = invoice.Items.Select(item => new SalesInvoiceItemFormViewModel
        {
            ProductId = item.ProductId,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice,
            Discount = item.Discount,
            VatRate = item.VatRate,
            LineTotal = item.LineTotal
        }).ToList()
    };

    private async Task<string> GenerateInvoiceNumberAsync()
    {
        var settings = await _context.CompanySettings.AsNoTracking().FirstOrDefaultAsync();
        var prefix = settings?.InvoicePrefix ?? "INV";
        var next = await _context.SalesInvoices.CountAsync() + 1;
        return $"{prefix}-{DateTime.Today:yyyy}-{next:00000}";
    }
}

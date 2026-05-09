using bestgen.Data;
using bestgen.Models;
using bestgen.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services;

public class PurchaseInvoiceService
{
    private readonly ApplicationDbContext _context;
    private readonly InvoiceCalculationService _calculationService;
    private readonly InventoryService _inventoryService;
    private readonly AccountingService _accountingService;
    private readonly PartyBalanceService _partyBalanceService;

    public PurchaseInvoiceService(
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

    public async Task<PurchaseInvoice> CreateAsync(PurchaseInvoiceFormViewModel model)
    {
        _calculationService.CalculatePurchase(model);

        var invoice = new PurchaseInvoice
        {
            PurchaseInvoiceNumber = string.IsNullOrWhiteSpace(model.PurchaseInvoiceNumber)
                ? await GenerateInvoiceNumberAsync()
                : model.PurchaseInvoiceNumber,
            SupplierInvoiceReference = model.SupplierInvoiceReference,
            InvoiceDate = model.InvoiceDate,
            SupplierId = model.SupplierId,
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
            Items = model.Items.Select(item => new PurchaseInvoiceItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitCost = item.UnitCost,
                Discount = item.Discount,
                VatRate = item.VatRate,
                LineTotal = item.LineTotal
            }).ToList()
        };

        _context.PurchaseInvoices.Add(invoice);
        if (invoice.Status is PurchaseInvoiceStatus.Received or PurchaseInvoiceStatus.PartiallyPaid or PurchaseInvoiceStatus.Paid)
        {
            await _inventoryService.IncreaseStockAsync(invoice);
        }
        await _accountingService.CreatePurchaseJournalEntryAsync(invoice);
        await _context.SaveChangesAsync();

        await _partyBalanceService.RecalculateSupplierAsync(invoice.SupplierId);
        await _context.SaveChangesAsync();
        return invoice;
    }

    public async Task UpdateAsync(int id, PurchaseInvoiceFormViewModel model)
    {
        _calculationService.CalculatePurchase(model);

        var invoice = await _context.PurchaseInvoices
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (invoice is null)
        {
            throw new InvalidOperationException("Purchase invoice was not found.");
        }

        invoice.SupplierInvoiceReference = model.SupplierInvoiceReference;
        invoice.InvoiceDate = model.InvoiceDate;
        invoice.SupplierId = model.SupplierId;
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
        invoice.UpdatedAt = DateTime.UtcNow;

        _context.PurchaseInvoiceItems.RemoveRange(invoice.Items);
        var newItems = model.Items.Select(item => new PurchaseInvoiceItem
        {
            PurchaseInvoiceId = invoice.Id,
            ProductId = item.ProductId,
            Quantity = item.Quantity,
            UnitCost = item.UnitCost,
            Discount = item.Discount,
            VatRate = item.VatRate,
            LineTotal = item.LineTotal
        }).ToList();
        await _context.PurchaseInvoiceItems.AddRangeAsync(newItems);

        await _context.SaveChangesAsync();

        await _partyBalanceService.RecalculateSupplierAsync(invoice.SupplierId);
        await _context.SaveChangesAsync();
    }

    public PurchaseInvoiceFormViewModel ToFormViewModel(PurchaseInvoice invoice) => new()
    {
        Id = invoice.Id,
        PurchaseInvoiceNumber = invoice.PurchaseInvoiceNumber,
        SupplierInvoiceReference = invoice.SupplierInvoiceReference,
        InvoiceDate = invoice.InvoiceDate,
        SupplierId = invoice.SupplierId,
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
        Items = invoice.Items.Select(item => new PurchaseInvoiceItemFormViewModel
        {
            ProductId = item.ProductId,
            Quantity = item.Quantity,
            UnitCost = item.UnitCost,
            Discount = item.Discount,
            VatRate = item.VatRate,
            LineTotal = item.LineTotal
        }).ToList()
    };

    private async Task<string> GenerateInvoiceNumberAsync()
    {
        var settings = await _context.CompanySettings.AsNoTracking().FirstOrDefaultAsync();
        var prefix = settings?.PurchaseInvoicePrefix ?? "PINV";
        var next = await _context.PurchaseInvoices.CountAsync() + 1;
        return $"{prefix}-{DateTime.Today:yyyy}-{next:00000}";
    }
}

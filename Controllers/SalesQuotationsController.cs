using bestgen.Data;
using bestgen.Models;
using bestgen.Services;
using bestgen.Services.InvoicePdf;
using bestgen.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Controllers;

[Authorize]
public class SalesQuotationsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly SalesQuotationService _service;
    private readonly InvoiceCalculationService _calculationService;
    private readonly InvoicePdfService _pdfService;

    public SalesQuotationsController(
        ApplicationDbContext context,
        SalesQuotationService service,
        InvoiceCalculationService calculationService,
        InvoicePdfService pdfService)
    {
        _context = context;
        _service = service;
        _calculationService = calculationService;
        _pdfService = pdfService;
    }

    public async Task<IActionResult> Index(string? q, string? status)
    {
        var query = _context.SalesQuotations.AsNoTracking().Include(x => x.Customer).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var t = q.Trim();
            query = query.Where(x => EF.Functions.Like(x.QuotationNumber, $"%{t}%")
                                  || (x.Customer != null && (EF.Functions.Like(x.Customer.NameAr, $"%{t}%") || (x.Customer.NameEn != null && EF.Functions.Like(x.Customer.NameEn, $"%{t}%")))));
        }
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<QuotationStatus>(status, true, out var st))
        {
            query = query.Where(x => x.Status == st);
        }
        var rows = await query.OrderByDescending(x => x.QuotationDate).ToListAsync();
        return View(rows);
    }

    public async Task<IActionResult> Details(int id)
    {
        var q = await _context.SalesQuotations.AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (q is null) return NotFound();
        return View(q);
    }

    [HttpGet]
    public async Task<IActionResult> Pdf(int id)
    {
        var result = await _pdfService.RenderSalesQuotationAsync(id);
        if (result is null) return NotFound();
        return File(result.Value.Bytes, "application/pdf", result.Value.FileName);
    }

    public async Task<IActionResult> Create()
    {
        await PopulateLookupsAsync();
        return View(new SalesQuotationFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [bestgen.Services.Idempotency.Idempotent]
    public async Task<IActionResult> Create(SalesQuotationFormViewModel model)
    {
        _calculationService.CalculateQuotation(model);
        if (model.Items.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Add at least one item.");
        }
        if (!ModelState.IsValid)
        {
            await PopulateLookupsAsync();
            return View(model);
        }
        var quotation = await _service.CreateAsync(model);
        return RedirectToAction(nameof(Details), new { id = quotation.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var quotation = await _context.SalesQuotations.AsNoTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (quotation is null) return NotFound();
        await PopulateLookupsAsync();
        return View(_service.ToFormViewModel(quotation));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, SalesQuotationFormViewModel model)
    {
        _calculationService.CalculateQuotation(model);
        if (model.Items.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Add at least one item.");
        }
        if (!ModelState.IsValid)
        {
            await PopulateLookupsAsync();
            return View(model);
        }
        await _service.UpdateAsync(id, model);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [bestgen.Services.Authorization.RequirePermission("quotations.delete")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var quotation = await _context.SalesQuotations.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);
        if (quotation is null) return NotFound();
        _context.SalesQuotationItems.RemoveRange(quotation.Items);
        _context.SalesQuotations.Remove(quotation);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [bestgen.Services.Idempotency.Idempotent]
    [bestgen.Services.Authorization.RequirePermission("invoices.create")]
    public async Task<IActionResult> ConvertToInvoice(int id, [FromServices] SalesInvoiceService invoiceService)
    {
        var quotation = await _context.SalesQuotations.AsNoTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (quotation is null) return NotFound();

        var firstWarehouse = await _context.Warehouses.AsNoTracking().OrderBy(w => w.Id).FirstOrDefaultAsync();
        if (firstWarehouse is null)
        {
            TempData["QuotationError"] = "Create a warehouse first before converting to an invoice.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var vm = new SalesInvoiceFormViewModel
        {
            InvoiceDate = DateTime.UtcNow.Date,
            CustomerId = quotation.CustomerId,
            WarehouseId = firstWarehouse.Id,
            PaymentMethod = PaymentMethod.Credit,
            Status = InvoiceStatus.Issued,
            Notes = $"From quotation {quotation.QuotationNumber}",
            Items = quotation.Items.Select(i => new SalesInvoiceItemFormViewModel
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                Discount = i.Discount,
                VatRate = i.VatRate
            }).ToList()
        };
        var invoice = await invoiceService.CreateAsync(vm);

        quotation = await _context.SalesQuotations.FirstAsync(x => x.Id == id);
        quotation.Status = QuotationStatus.Accepted;
        await _context.SaveChangesAsync();

        TempData["QuotationMessage"] = $"Converted to invoice {invoice.InvoiceNumber}.";
        return RedirectToAction("Details", "SalesInvoices", new { id = invoice.Id });
    }

    private async Task PopulateLookupsAsync()
    {
        ViewBag.Customers = await _context.Customers.AsNoTracking().OrderBy(c => c.NameAr).ToListAsync();
        ViewBag.Products = await _context.Products.AsNoTracking().OrderBy(p => p.NameAr).ToListAsync();
        ViewBag.Statuses = Enum.GetValues<QuotationStatus>();
    }
}

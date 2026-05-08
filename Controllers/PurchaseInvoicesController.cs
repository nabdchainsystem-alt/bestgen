using bestgen.Data;
using bestgen.Helpers;
using bestgen.Models;
using bestgen.Services;
using bestgen.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Controllers;

[Authorize]
public class PurchaseInvoicesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly PurchaseInvoiceService _purchaseInvoiceService;
    private readonly InvoiceCalculationService _calculationService;

    public PurchaseInvoicesController(
        ApplicationDbContext context,
        PurchaseInvoiceService purchaseInvoiceService,
        InvoiceCalculationService calculationService)
    {
        _context = context;
        _purchaseInvoiceService = purchaseInvoiceService;
        _calculationService = calculationService;
    }

    public async Task<IActionResult> Index(string? q, string? status)
    {
        ViewBag.CurrentSearch = q;
        ViewBag.CurrentStatus = status;
        ViewBag.StatusOptions = Enum.GetValues<PurchaseInvoiceStatus>()
            .Select(value => new SelectListItem { Text = EntityDisplayHelper.TranslateEnum(value), Value = value.ToString() })
            .ToList();

        var query = _context.PurchaseInvoices
            .AsNoTracking()
            .Include(invoice => invoice.Supplier)
            .Include(invoice => invoice.Warehouse)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(invoice =>
                invoice.PurchaseInvoiceNumber.Contains(q)
                || (invoice.SupplierInvoiceReference != null && invoice.SupplierInvoiceReference.Contains(q))
                || invoice.Supplier!.NameAr.Contains(q)
                || (invoice.Supplier.NameEn != null && invoice.Supplier.NameEn.Contains(q)));
        }

        if (Enum.TryParse<PurchaseInvoiceStatus>(status, out var invoiceStatus))
        {
            query = query.Where(invoice => invoice.Status == invoiceStatus);
        }

        var invoices = await query.OrderByDescending(invoice => invoice.InvoiceDate).ToListAsync();
        return View(invoices);
    }

    public async Task<IActionResult> Details(int id)
    {
        var invoice = await _context.PurchaseInvoices
            .AsNoTracking()
            .Include(x => x.Supplier)
            .Include(x => x.Warehouse)
            .Include(x => x.Items)
            .ThenInclude(item => item.Product)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (invoice is null)
        {
            return NotFound();
        }

        return View(invoice);
    }

    public async Task<IActionResult> Create()
    {
        await PopulateInvoiceLookupsAsync();
        return View(new PurchaseInvoiceFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PurchaseInvoiceFormViewModel model)
    {
        _calculationService.CalculatePurchase(model);
        ValidateItems(model.Items.Count);

        if (!ModelState.IsValid)
        {
            await PopulateInvoiceLookupsAsync();
            return View(model);
        }

        var invoice = await _purchaseInvoiceService.CreateAsync(model);
        return RedirectToAction(nameof(Details), new { id = invoice.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var invoice = await _context.PurchaseInvoices
            .AsNoTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (invoice is null)
        {
            return NotFound();
        }

        await PopulateInvoiceLookupsAsync();
        return View(_purchaseInvoiceService.ToFormViewModel(invoice));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PurchaseInvoiceFormViewModel model)
    {
        _calculationService.CalculatePurchase(model);
        ValidateItems(model.Items.Count);

        if (!ModelState.IsValid)
        {
            await PopulateInvoiceLookupsAsync();
            return View(model);
        }

        await _purchaseInvoiceService.UpdateAsync(id, model);
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Delete(int id)
    {
        var invoice = await _context.PurchaseInvoices
            .AsNoTracking()
            .Include(x => x.Supplier)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (invoice is null)
        {
            return NotFound();
        }

        return View(invoice);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var invoice = await _context.PurchaseInvoices.FindAsync(id);
        if (invoice is not null)
        {
            _context.PurchaseInvoices.Remove(invoice);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateInvoiceLookupsAsync()
    {
        ViewBag.Suppliers = await _context.Suppliers.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.NameAr).ToListAsync();
        ViewBag.Warehouses = await _context.Warehouses.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();
        ViewBag.Products = await _context.Products.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.NameAr).ToListAsync();
        ViewBag.PaymentMethods = Enum.GetValues<PaymentMethod>();
        ViewBag.Statuses = Enum.GetValues<PurchaseInvoiceStatus>();
    }

    private void ValidateItems(int itemCount)
    {
        if (itemCount == 0)
        {
            ModelState.AddModelError("Items", "أضف صنفا واحدا على الأقل للفاتورة.");
        }
    }
}

using bestgen.Data;
using bestgen.Helpers;
using bestgen.Models;
using bestgen.Services;
using bestgen.Services.InvoicePdf;
using bestgen.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Controllers;

[Authorize]
public class SalesInvoicesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly SalesInvoiceService _salesInvoiceService;
    private readonly InvoiceCalculationService _calculationService;
    private readonly InvoicePdfService _pdfService;

    public SalesInvoicesController(
        ApplicationDbContext context,
        SalesInvoiceService salesInvoiceService,
        InvoiceCalculationService calculationService,
        InvoicePdfService pdfService)
    {
        _context = context;
        _salesInvoiceService = salesInvoiceService;
        _calculationService = calculationService;
        _pdfService = pdfService;
    }

    [HttpGet]
    public async Task<IActionResult> Pdf(int id)
    {
        var result = await _pdfService.RenderSalesInvoiceAsync(id);
        if (result is null) return NotFound();
        return File(result.Value.Bytes, "application/pdf", result.Value.FileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [bestgen.Services.Idempotency.Idempotent]
    public async Task<IActionResult> SubmitForApproval(int id, [FromServices] ApprovalService approvals)
    {
        var invoice = await _context.SalesInvoices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (invoice is null) return NotFound();
        var name = User.Identity?.IsAuthenticated == true ? User.Identity!.Name : null;
        var uid = User.FindFirst("sub")?.Value;
        await approvals.SubmitAsync(bestgen.Models.ApprovalDocumentType.SalesInvoice,
            invoice.Id, invoice.InvoiceNumber, invoice.GrandTotal, uid, name);
        TempData["ApprovalMessage"] = $"Submitted {invoice.InvoiceNumber} for approval.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [bestgen.Services.Idempotency.Idempotent]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("delivery")]
    public async Task<IActionResult> Send(
        int id,
        bestgen.Models.DeliveryChannel channel,
        string recipient,
        bool useTemplate,
        [FromServices] bestgen.Services.Delivery.InvoiceDeliveryService delivery)
    {
        if (string.IsNullOrWhiteSpace(recipient))
        {
            TempData["DeliveryError"] = "Please provide a recipient.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var userId = User.Identity?.IsAuthenticated == true ? User.FindFirst("sub")?.Value ?? User.Identity!.Name : null;
        var result = await delivery.SendSalesInvoiceAsync(id, channel, recipient.Trim(), userId, useTemplate);

        if (result.Success)
        {
            TempData["DeliveryMessage"] = channel == bestgen.Models.DeliveryChannel.Email
                ? $"Invoice emailed to {recipient}."
                : $"Invoice sent on WhatsApp to {recipient}.";
        }
        else
        {
            TempData["DeliveryError"] = result.Error;
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [bestgen.Services.Idempotency.Idempotent]
    public async Task<IActionResult> GenerateZatca(int id, [FromServices] bestgen.Services.Zatca.ZatcaService zatca)
    {
        await zatca.GenerateAsync(id);
        TempData["ZatcaMessage"] = "ZATCA Phase 2 e-invoice generated successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [bestgen.Services.Idempotency.Idempotent]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("zatca")]
    public async Task<IActionResult> SubmitZatca(int id, [FromServices] bestgen.Services.Zatca.ZatcaService zatca)
    {
        var ei = await _context.EInvoices.FirstOrDefaultAsync(x => x.SalesInvoiceId == id);
        if (ei is null) return NotFound();
        await zatca.SubmitAsync(ei.Id);
        TempData["ZatcaMessage"] = "Submitted to FATOORA (stub — wire real HTTP integration in ZatcaService).";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> ZatcaXml(int id)
    {
        var ei = await _context.EInvoices.AsNoTracking().FirstOrDefaultAsync(x => x.SalesInvoiceId == id);
        if (ei?.Xml is null) return NotFound();
        return File(System.Text.Encoding.UTF8.GetBytes(ei.Xml), "application/xml", $"{ei.Uuid}.xml");
    }

    [HttpGet]
    public async Task<IActionResult> ZatcaQr(int id, [FromServices] bestgen.Services.Zatca.ZatcaService zatca)
    {
        var ei = await _context.EInvoices.AsNoTracking().FirstOrDefaultAsync(x => x.SalesInvoiceId == id);
        if (ei is null) return NotFound();
        var png = zatca.GetQrPng(ei);
        if (png is null) return NotFound();
        return File(png, "image/png");
    }

    public async Task<IActionResult> Index(string? q, string? status)
    {
        ViewBag.CurrentSearch = q;
        ViewBag.CurrentStatus = status;
        ViewBag.StatusOptions = Enum.GetValues<InvoiceStatus>()
            .Select(value => new SelectListItem { Text = EntityDisplayHelper.TranslateEnum(value), Value = value.ToString() })
            .ToList();

        var query = _context.SalesInvoices
            .AsNoTracking()
            .Include(invoice => invoice.Customer)
            .Include(invoice => invoice.Warehouse)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(invoice =>
                invoice.InvoiceNumber.Contains(q)
                || invoice.Customer!.NameAr.Contains(q)
                || (invoice.Customer.NameEn != null && invoice.Customer.NameEn.Contains(q)));
        }

        if (Enum.TryParse<InvoiceStatus>(status, out var invoiceStatus))
        {
            query = query.Where(invoice => invoice.Status == invoiceStatus);
        }

        var invoices = await query.OrderByDescending(invoice => invoice.InvoiceDate).ToListAsync();
        return View(invoices);
    }

    public async Task<IActionResult> Details(int id)
    {
        var invoice = await _context.SalesInvoices
            .AsNoTracking()
            .Include(x => x.Customer)
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
        return View(new SalesInvoiceFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [bestgen.Services.Idempotency.Idempotent]
    public async Task<IActionResult> Create(SalesInvoiceFormViewModel model)
    {
        _calculationService.CalculateSales(model);
        ValidateItems(model.Items.Count);

        if (!ModelState.IsValid)
        {
            await PopulateInvoiceLookupsAsync();
            return View(model);
        }

        var invoice = await _salesInvoiceService.CreateAsync(model);
        return RedirectToAction(nameof(Details), new { id = invoice.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var invoice = await _context.SalesInvoices
            .AsNoTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (invoice is null)
        {
            return NotFound();
        }

        await PopulateInvoiceLookupsAsync();
        return View(_salesInvoiceService.ToFormViewModel(invoice));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, SalesInvoiceFormViewModel model)
    {
        _calculationService.CalculateSales(model);
        ValidateItems(model.Items.Count);

        if (!ModelState.IsValid)
        {
            await PopulateInvoiceLookupsAsync();
            return View(model);
        }

        await _salesInvoiceService.UpdateAsync(id, model);
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Delete(int id)
    {
        var invoice = await _context.SalesInvoices
            .AsNoTracking()
            .Include(x => x.Customer)
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
        var invoice = await _context.SalesInvoices.FindAsync(id);
        if (invoice is not null)
        {
            _context.SalesInvoices.Remove(invoice);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateInvoiceLookupsAsync()
    {
        ViewBag.Customers = await _context.Customers.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.NameAr).ToListAsync();
        ViewBag.Warehouses = await _context.Warehouses.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();
        ViewBag.Products = await _context.Products.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.NameAr).ToListAsync();
        ViewBag.PaymentMethods = Enum.GetValues<PaymentMethod>();
        ViewBag.Statuses = Enum.GetValues<InvoiceStatus>();
    }

    private void ValidateItems(int itemCount)
    {
        if (itemCount == 0)
        {
            var isArabic = System.Globalization.CultureInfo.CurrentUICulture.Name
                .StartsWith("ar", StringComparison.OrdinalIgnoreCase);
            ModelState.AddModelError("Items", isArabic
                ? "أضف صنفا واحدا على الأقل للفاتورة."
                : "Add at least one item to the invoice.");
        }
    }
}

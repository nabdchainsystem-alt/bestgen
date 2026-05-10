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
public class PurchaseInvoicesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly PurchaseInvoiceService _purchaseInvoiceService;
    private readonly InvoiceCalculationService _calculationService;
    private readonly InvoicePdfService _pdfService;

    public PurchaseInvoicesController(
        ApplicationDbContext context,
        PurchaseInvoiceService purchaseInvoiceService,
        InvoiceCalculationService calculationService,
        InvoicePdfService pdfService)
    {
        _context = context;
        _purchaseInvoiceService = purchaseInvoiceService;
        _calculationService = calculationService;
        _pdfService = pdfService;
    }

    [HttpGet]
    public async Task<IActionResult> Pdf(int id)
    {
        var result = await _pdfService.RenderPurchaseInvoiceAsync(id);
        if (result is null) return NotFound();
        return File(result.Value.Bytes, "application/pdf", result.Value.FileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [bestgen.Services.Idempotency.Idempotent]
    public async Task<IActionResult> SubmitForApproval(int id, [FromServices] ApprovalService approvals)
    {
        var invoice = await _context.PurchaseInvoices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (invoice is null) return NotFound();
        var name = User.Identity?.IsAuthenticated == true ? User.Identity!.Name : null;
        var uid = User.FindFirst("sub")?.Value;
        await approvals.SubmitAsync(bestgen.Models.ApprovalDocumentType.PurchaseInvoice,
            invoice.Id, invoice.PurchaseInvoiceNumber, invoice.GrandTotal, uid, name);
        TempData["ApprovalMessage"] = $"Submitted {invoice.PurchaseInvoiceNumber} for approval.";
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
        var result = await delivery.SendPurchaseInvoiceAsync(id, channel, recipient.Trim(), userId, useTemplate);

        if (result.Success)
        {
            TempData["DeliveryMessage"] = channel == bestgen.Models.DeliveryChannel.Email
                ? $"Purchase invoice emailed to {recipient}."
                : $"Purchase invoice sent on WhatsApp to {recipient}.";
        }
        else
        {
            TempData["DeliveryError"] = result.Error;
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    // ============================================================
    // AI invoice scanning — Claude Vision extracts a supplier invoice
    // ============================================================

    [HttpGet]
    public IActionResult Scan() => View(new bestgen.Services.Ai.ExtractedInvoice());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(35_000_000)]
    public async Task<IActionResult> Scan(IFormFile file, [FromServices] bestgen.Services.Ai.InvoiceExtractionService extractor)
    {
        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError("file", "Please choose a PDF or image file.");
            return View(new bestgen.Services.Ai.ExtractedInvoice());
        }

        var media = (file.ContentType ?? "").ToLowerInvariant();
        var allowed = new[] { "application/pdf", "image/jpeg", "image/png", "image/webp", "image/gif" };
        if (!allowed.Contains(media))
        {
            ModelState.AddModelError("file", "Unsupported file type. Use PDF, JPEG, PNG, or WEBP.");
            return View(new bestgen.Services.Ai.ExtractedInvoice());
        }

        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var bytes = ms.ToArray();

        try
        {
            var extracted = await extractor.ExtractAsync(bytes, media);
            ViewBag.Extracted = true;
            return View(extracted);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(new bestgen.Services.Ai.ExtractedInvoice());
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFromScan(string extractedJson)
    {
        if (string.IsNullOrWhiteSpace(extractedJson))
        {
            return RedirectToAction(nameof(Scan));
        }

        var extracted = System.Text.Json.JsonSerializer.Deserialize<bestgen.Services.Ai.ExtractedInvoice>(
            extractedJson,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
            });

        if (extracted is null)
        {
            TempData["ScanError"] = "Could not read the extracted data.";
            return RedirectToAction(nameof(Scan));
        }

        // 1. Match or create supplier
        var supplier = await ResolveSupplierAsync(extracted);

        // 2. Pick the first warehouse as default
        var warehouseId = await _context.Warehouses
            .OrderBy(w => w.Id)
            .Select(w => w.Id)
            .FirstOrDefaultAsync();
        if (warehouseId == 0)
        {
            TempData["ScanError"] = "Create a warehouse first before scanning invoices.";
            return RedirectToAction(nameof(Scan));
        }

        // 3. Build the form view-model with resolved/created products
        var vm = new PurchaseInvoiceFormViewModel
        {
            InvoiceDate = ParseDateOrToday(extracted.InvoiceDate),
            SupplierId = supplier.Id,
            WarehouseId = warehouseId,
            SupplierInvoiceReference = extracted.InvoiceNumber,
            Notes = extracted.Notes,
            PaymentMethod = PaymentMethod.Credit,
            Status = PurchaseInvoiceStatus.Draft,
            Items = new List<PurchaseInvoiceItemFormViewModel>()
        };

        foreach (var line in extracted.Lines.Where(l => !string.IsNullOrWhiteSpace(l.Description)))
        {
            var product = await ResolveProductAsync(line.Description!);
            vm.Items.Add(new PurchaseInvoiceItemFormViewModel
            {
                ProductId = product.Id,
                Quantity  = line.Quantity is > 0 ? line.Quantity!.Value : 1m,
                UnitCost  = line.UnitPrice ?? 0m,
                Discount  = 0m,
                VatRate   = line.VatRate ?? extracted.VatRate ?? 15m
            });
        }
        if (vm.Items.Count == 0)
        {
            vm.Items.Add(new PurchaseInvoiceItemFormViewModel());
        }

        _calculationService.CalculatePurchase(vm);

        var invoice = await _purchaseInvoiceService.CreateAsync(vm);
        TempData["ZatcaMessage"] = $"Draft created from AI extraction: {invoice.PurchaseInvoiceNumber}. Review and post.";
        return RedirectToAction(nameof(Details), new { id = invoice.Id });
    }

    private async Task<Supplier> ResolveSupplierAsync(bestgen.Services.Ai.ExtractedInvoice extracted)
    {
        var name = (extracted.SupplierName ?? "").Trim();
        if (string.IsNullOrEmpty(name)) name = "AI-extracted supplier";

        var existing = await _context.Suppliers.FirstOrDefaultAsync(s => s.NameAr == name || s.NameEn == name);
        if (existing is not null) return existing;

        // Generate a unique supplier code
        var nextSeq = await _context.Suppliers.CountAsync() + 1;
        var code = $"AI-{nextSeq:D4}";

        var supplier = new Supplier
        {
            SupplierCode = code,
            NameAr = name,
            NameEn = name,
            VatNumber = extracted.SupplierVatNumber,
            Address = extracted.SupplierAddress,
            Phone = extracted.SupplierPhone,
            IsActive = true
        };
        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync();
        return supplier;
    }

    private async Task<Product> ResolveProductAsync(string description)
    {
        var name = description.Trim();
        var existing = await _context.Products.FirstOrDefaultAsync(p => p.NameAr == name || p.NameEn == name);
        if (existing is not null) return existing;

        var nextSeq = await _context.Products.CountAsync() + 1;
        var sku = $"AI-{nextSeq:D5}";

        var product = new Product
        {
            SKU = sku,
            NameAr = name,
            NameEn = name,
            SellingPrice = 0,
            PurchasePrice = 0,
            CurrentStock = 0,
            MinimumStockLevel = 0,
            IsActive = true,
            TrackInventory = true
        };
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        return product;
    }

    private static DateTime ParseDateOrToday(string? s)
    {
        if (DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeLocal, out var d)) return d.Date;
        return DateTime.Today;
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
            var isArabic = System.Globalization.CultureInfo.CurrentUICulture.Name
                .StartsWith("ar", StringComparison.OrdinalIgnoreCase);
            ModelState.AddModelError("Items", isArabic
                ? "أضف صنفا واحدا على الأقل للفاتورة."
                : "Add at least one item to the invoice.");
        }
    }
}

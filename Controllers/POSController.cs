using bestgen.Data;
using bestgen.Models;
using bestgen.Services;
using bestgen.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Controllers;

[Authorize]
public class POSController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly SalesInvoiceService _sales;

    public POSController(ApplicationDbContext db, SalesInvoiceService sales)
    {
        _db = db;
        _sales = sales;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.Categories = await _db.ProductCategories.AsNoTracking().OrderBy(c => c.NameAr).ToListAsync();
        ViewBag.Products = await _db.Products.AsNoTracking()
            .OrderBy(p => p.NameAr)
            .Take(60)
            .Select(p => new POSProductDto(p.Id, p.SKU, p.NameAr, p.NameEn, p.SellingPrice, p.VatRate, p.Category, p.CategoryId))
            .ToListAsync();
        ViewBag.Customers = await _db.Customers.AsNoTracking().OrderBy(c => c.NameAr).Take(200).ToListAsync();
        ViewBag.Warehouses = await _db.Warehouses.AsNoTracking().OrderBy(w => w.Name).ToListAsync();
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Search(string? q, int? categoryId)
    {
        var query = _db.Products.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(p => EF.Functions.Like(p.NameAr, $"%{term}%")
                                  || (p.NameEn != null && EF.Functions.Like(p.NameEn, $"%{term}%"))
                                  || EF.Functions.Like(p.SKU, $"%{term}%")
                                  || (p.Barcode != null && EF.Functions.Like(p.Barcode, $"%{term}%")));
        }
        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }
        var rows = await query
            .OrderBy(p => p.NameAr)
            .Take(120)
            .Select(p => new POSProductDto(p.Id, p.SKU, p.NameAr, p.NameEn, p.SellingPrice, p.VatRate, p.Category, p.CategoryId))
            .ToListAsync();
        return Json(rows);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [bestgen.Services.Idempotency.Idempotent]
    public async Task<IActionResult> Checkout([FromBody] POSCheckoutRequest req)
    {
        if (req is null || req.Lines is null || req.Lines.Count == 0)
        {
            return BadRequest(new { error = "Cart is empty." });
        }
        if (req.WarehouseId <= 0)
        {
            return BadRequest(new { error = "Warehouse missing." });
        }

        var customerId = req.CustomerId;
        if (customerId <= 0)
        {
            customerId = await EnsureWalkInCustomerAsync();
        }

        var vm = new SalesInvoiceFormViewModel
        {
            InvoiceDate = DateTime.UtcNow.Date,
            CustomerId = customerId,
            WarehouseId = req.WarehouseId,
            PaymentMethod = req.PaymentMethod,
            Status = req.PaymentMethod == PaymentMethod.Credit
                ? InvoiceStatus.Issued
                : InvoiceStatus.Paid,
            PaidAmount = 0m,
            Notes = string.IsNullOrWhiteSpace(req.Notes) ? "POS sale" : $"POS sale — {req.Notes}",
            Items = req.Lines.Select(l => new SalesInvoiceItemFormViewModel
            {
                ProductId = l.ProductId,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                Discount = l.Discount,
                VatRate = l.VatRate
            }).ToList()
        };

        // Mark as paid in full when cash/card.
        if (req.PaymentMethod != PaymentMethod.Credit)
        {
            // Calculation runs inside CreateAsync; PaidAmount will be ignored if status calc says otherwise.
            vm.PaidAmount = 0m;
        }

        var invoice = await _sales.CreateAsync(vm);

        if (req.PaymentMethod != PaymentMethod.Credit)
        {
            // For cash/card, mark fully paid by writing back the totals on the saved invoice.
            invoice.PaidAmount = invoice.GrandTotal;
            invoice.RemainingAmount = 0;
            invoice.Status = InvoiceStatus.Paid;
            await _db.SaveChangesAsync();
        }

        bestgen.Services.Observability.BestgenMetrics.PosSales.WithLabels(req.PaymentMethod.ToString().ToLowerInvariant()).Inc();
        return Json(new
        {
            ok = true,
            invoiceId = invoice.Id,
            invoiceNumber = invoice.InvoiceNumber,
            grandTotal = invoice.GrandTotal,
            change = req.PaymentMethod == PaymentMethod.Cash ? Math.Max(0m, req.AmountTendered - invoice.GrandTotal) : 0m
        });
    }

    private async Task<int> EnsureWalkInCustomerAsync()
    {
        var existing = await _db.Customers.FirstOrDefaultAsync(c => c.NameAr == "عميل عابر" || c.NameAr == "عميل نقدي");
        if (existing is not null) return existing.Id;
        var walk = new Customer
        {
            NameAr = "عميل عابر",
            NameEn = "Walk-in",
            CustomerCode = "POS-WALKIN"
        };
        _db.Customers.Add(walk);
        await _db.SaveChangesAsync();
        return walk.Id;
    }

    public sealed record POSProductDto(int Id, string Sku, string NameAr, string? NameEn, decimal SellingPrice, decimal VatRate, string? Category, int? CategoryId);

    public sealed class POSCheckoutRequest
    {
        public int CustomerId { get; set; }
        public int WarehouseId { get; set; }
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
        public decimal AmountTendered { get; set; }
        public string? Notes { get; set; }
        public List<POSCheckoutLine> Lines { get; set; } = new();
    }

    public sealed class POSCheckoutLine
    {
        public int ProductId { get; set; }
        public decimal Quantity { get; set; } = 1m;
        public decimal UnitPrice { get; set; }
        public decimal Discount { get; set; }
        public decimal VatRate { get; set; } = 15m;
    }
}

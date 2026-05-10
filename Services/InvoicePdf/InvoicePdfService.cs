using System.Globalization;
using bestgen.Data;
using bestgen.Helpers;
using bestgen.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;

namespace bestgen.Services.InvoicePdf;

/// <summary>
/// Loads a Sales/Purchase invoice and renders it to a PDF byte stream
/// using <see cref="InvoicePdfDocument"/>. Includes ZATCA Phase 1 QR code
/// based on the seller's CompanySettings + the invoice totals.
/// </summary>
public class InvoicePdfService
{
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;

    public InvoicePdfService(ApplicationDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public async Task<(byte[] Bytes, string FileName)?> RenderSalesInvoiceAsync(int invoiceId)
    {
        var invoice = await _db.SalesInvoices
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Warehouse)
            .Include(x => x.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(x => x.Id == invoiceId);

        if (invoice is null) return null;

        var company = await GetCompanySettingsAsync();
        var isArabic = IsArabicCulture();

        var model = new InvoicePdfModel
        {
            DocumentTitle  = isArabic ? "فاتورة ضريبية" : "Tax Invoice",
            DocumentNumber = invoice.InvoiceNumber,
            DocumentDate   = invoice.InvoiceDate,
            StatusLabel    = EntityDisplayHelper.TranslateEnum(invoice.Status),

            SellerName                   = isArabic ? company.CompanyNameAr : (company.CompanyNameEn ?? company.CompanyNameAr),
            SellerNameEn                 = company.CompanyNameEn,
            SellerVatNumber              = company.VatNumber,
            SellerCommercialRegistration = company.CommercialRegistrationNumber,
            SellerAddress                = company.Address,
            SellerCity                   = company.City,
            SellerCountry                = company.Country,
            SellerLogoPath               = company.LogoPath,

            CounterpartyHeading    = isArabic ? "إلى السادة" : "Bill To",
            CounterpartyName       = invoice.Customer?.NameAr ?? "—",
            CounterpartyVatNumber  = invoice.Customer?.VatNumber,
            CounterpartyAddress    = invoice.Customer?.Address,
            CounterpartyPhone      = invoice.Customer?.Phone,

            Warehouse     = invoice.Warehouse?.Name,
            Notes         = invoice.Notes,
            FooterText    = isArabic ? company.InvoiceFooterAr : (company.InvoiceFooterEn ?? company.InvoiceFooterAr),

            Lines = invoice.Items.Select(i => new InvoicePdfLine
            {
                ProductName = i.Product?.NameAr ?? "—",
                ProductSku  = i.Product?.SKU,
                Quantity    = i.Quantity,
                UnitPrice   = i.UnitPrice,
                Discount    = i.Discount,
                VatRate     = i.VatRate,
                LineTotal   = i.LineTotal
            }).ToList(),

            Subtotal      = invoice.Subtotal,
            DiscountTotal = invoice.DiscountTotal,
            VatTotal      = invoice.VatTotal,
            GrandTotal    = invoice.GrandTotal,
            CurrencySymbol = company.CurrencySymbol
        };

        // Prefer the Phase 2 QR from a generated EInvoice (it includes the
        // crypto stamp + cert info) over the Phase 1 fallback.
        byte[]? overrideQr = null;
        var eInvoice = await _db.EInvoices.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SalesInvoiceId == invoiceId);
        if (eInvoice?.QrBase64 is { Length: > 0 })
        {
            try
            {
                using var gen = new QRCoder.QRCodeGenerator();
                using var data = gen.CreateQrCode(eInvoice.QrBase64, QRCoder.QRCodeGenerator.ECCLevel.M);
                var qr = new QRCoder.PngByteQRCode(data);
                overrideQr = qr.GetGraphic(14);
            }
            catch { /* fall back to Phase 1 */ }
        }

        var bytes = Render(model, isArabic, company, overrideQr);
        var fileName = $"{invoice.InvoiceNumber}.pdf";
        return (bytes, fileName);
    }

    public async Task<(byte[] Bytes, string FileName)?> RenderSalesQuotationAsync(int quotationId)
    {
        var quotation = await _db.SalesQuotations
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(x => x.Id == quotationId);

        if (quotation is null) return null;

        var company = await GetCompanySettingsAsync();
        var isArabic = IsArabicCulture();

        var notes = string.IsNullOrWhiteSpace(quotation.Terms)
            ? quotation.Notes
            : (string.IsNullOrWhiteSpace(quotation.Notes) ? quotation.Terms : $"{quotation.Notes}\n\n{quotation.Terms}");

        var validUntilLabel = quotation.ValidUntil is { } v
            ? (isArabic ? $"ساري حتى: {v:yyyy-MM-dd}" : $"Valid until: {v:yyyy-MM-dd}")
            : null;

        var combinedNotes = string.IsNullOrWhiteSpace(validUntilLabel)
            ? notes
            : (string.IsNullOrWhiteSpace(notes) ? validUntilLabel : $"{validUntilLabel}\n{notes}");

        var model = new InvoicePdfModel
        {
            DocumentTitle  = isArabic ? "عرض سعر" : "Quotation",
            DocumentNumber = quotation.QuotationNumber,
            DocumentDate   = quotation.QuotationDate,
            StatusLabel    = EntityDisplayHelper.TranslateEnum(quotation.Status),

            SellerName                   = isArabic ? company.CompanyNameAr : (company.CompanyNameEn ?? company.CompanyNameAr),
            SellerNameEn                 = company.CompanyNameEn,
            SellerVatNumber              = company.VatNumber,
            SellerCommercialRegistration = company.CommercialRegistrationNumber,
            SellerAddress                = company.Address,
            SellerCity                   = company.City,
            SellerCountry                = company.Country,
            SellerLogoPath               = company.LogoPath,

            CounterpartyHeading    = isArabic ? "إلى السادة" : "Quote For",
            CounterpartyName       = quotation.Customer?.NameAr ?? "—",
            CounterpartyVatNumber  = quotation.Customer?.VatNumber,
            CounterpartyAddress    = quotation.Customer?.Address,
            CounterpartyPhone      = quotation.Customer?.Phone,

            Notes      = combinedNotes,
            FooterText = isArabic ? company.InvoiceFooterAr : (company.InvoiceFooterEn ?? company.InvoiceFooterAr),

            Lines = quotation.Items.Select(i => new InvoicePdfLine
            {
                ProductName = i.Product?.NameAr ?? "—",
                ProductSku  = i.Product?.SKU,
                Quantity    = i.Quantity,
                UnitPrice   = i.UnitPrice,
                Discount    = i.Discount,
                VatRate     = i.VatRate,
                LineTotal   = i.LineTotal
            }).ToList(),

            Subtotal      = quotation.Subtotal,
            DiscountTotal = quotation.DiscountTotal,
            VatTotal      = quotation.VatTotal,
            GrandTotal    = quotation.GrandTotal,
            CurrencySymbol = company.CurrencySymbol,

            // ZATCA QR is for tax invoices, not quotations.
            IncludeZatcaQr = false
        };

        var bytes = Render(model, isArabic, company);
        var fileName = $"{quotation.QuotationNumber}.pdf";
        return (bytes, fileName);
    }

    public async Task<(byte[] Bytes, string FileName)?> RenderPurchaseInvoiceAsync(int invoiceId)
    {
        var invoice = await _db.PurchaseInvoices
            .AsNoTracking()
            .Include(x => x.Supplier)
            .Include(x => x.Warehouse)
            .Include(x => x.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(x => x.Id == invoiceId);

        if (invoice is null) return null;

        var company = await GetCompanySettingsAsync();
        var isArabic = IsArabicCulture();

        var model = new InvoicePdfModel
        {
            DocumentTitle  = isArabic ? "فاتورة مشتريات" : "Purchase Invoice",
            DocumentNumber = invoice.PurchaseInvoiceNumber,
            DocumentDate   = invoice.InvoiceDate,
            StatusLabel    = EntityDisplayHelper.TranslateEnum(invoice.Status),

            SellerName                   = isArabic ? company.CompanyNameAr : (company.CompanyNameEn ?? company.CompanyNameAr),
            SellerNameEn                 = company.CompanyNameEn,
            SellerVatNumber              = company.VatNumber,
            SellerCommercialRegistration = company.CommercialRegistrationNumber,
            SellerAddress                = company.Address,
            SellerCity                   = company.City,
            SellerCountry                = company.Country,
            SellerLogoPath               = company.LogoPath,

            CounterpartyHeading   = isArabic ? "المورد" : "Supplier",
            CounterpartyName      = invoice.Supplier?.NameAr ?? "—",
            CounterpartyVatNumber = invoice.Supplier?.VatNumber,
            CounterpartyAddress   = invoice.Supplier?.Address,
            CounterpartyPhone     = invoice.Supplier?.Phone,

            Warehouse  = invoice.Warehouse?.Name,
            Notes      = invoice.Notes,
            FooterText = isArabic ? company.InvoiceFooterAr : (company.InvoiceFooterEn ?? company.InvoiceFooterAr),

            Lines = invoice.Items.Select(i => new InvoicePdfLine
            {
                ProductName = i.Product?.NameAr ?? "—",
                ProductSku  = i.Product?.SKU,
                Quantity    = i.Quantity,
                UnitPrice   = i.UnitCost,
                Discount    = i.Discount,
                VatRate     = i.VatRate,
                LineTotal   = i.LineTotal
            }).ToList(),

            Subtotal      = invoice.Subtotal,
            DiscountTotal = invoice.DiscountTotal,
            VatTotal      = invoice.VatTotal,
            GrandTotal    = invoice.GrandTotal,
            CurrencySymbol = company.CurrencySymbol,

            // ZATCA QR is for sales (B2C/B2B issued by us), not purchases. Hide
            // on purchase invoices to avoid implying we issued them.
            IncludeZatcaQr = false
        };

        var bytes = Render(model, isArabic, company);
        var fileName = $"{invoice.PurchaseInvoiceNumber}.pdf";
        return (bytes, fileName);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private byte[] Render(InvoicePdfModel model, bool isArabic, CompanySettings company, byte[]? overrideQr = null)
    {
        byte[]? qr = overrideQr;
        if (qr is null && model.IncludeZatcaQr && !string.IsNullOrWhiteSpace(company.VatNumber))
        {
            qr = ZatcaQrGenerator.GenerateQrPng(
                sellerName: model.SellerName,
                vatNumber: company.VatNumber,
                timestamp: model.DocumentDate,
                totalWithVat: model.GrandTotal,
                vatTotal: model.VatTotal);
        }

        byte[]? logo = LoadLogoBytes(company.LogoPath);

        var doc = new InvoicePdfDocument(model, isArabic, qr, logo);
        return doc.GeneratePdf();
    }

    private byte[]? LoadLogoBytes(string? logoPath)
    {
        // Prefer the company-configured logo; fall back to the shipped Bestgen
        // logo so PDFs look polished out of the box.
        var candidates = new[]
        {
            logoPath,
            "/img/logo.png",
            "/img/logo-transparent.png"
        };
        foreach (var p in candidates)
        {
            if (string.IsNullOrWhiteSpace(p)) continue;
            var rel = p.TrimStart('/', '~');
            var absolute = Path.Combine(_env.WebRootPath ?? "wwwroot", rel.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(absolute))
            {
                try { return File.ReadAllBytes(absolute); } catch { /* ignore */ }
            }
        }
        return null;
    }

    private async Task<CompanySettings> GetCompanySettingsAsync()
    {
        var company = await _db.CompanySettings.AsNoTracking().FirstOrDefaultAsync();
        return company ?? new CompanySettings
        {
            CompanyNameAr = "Bestgen ERP",
            Country = "Saudi Arabia",
            DefaultVatRate = 15,
            CurrencySymbol = "ر.س"
        };
    }

    private static bool IsArabicCulture() =>
        CultureInfo.CurrentUICulture.Name.StartsWith("ar", StringComparison.OrdinalIgnoreCase);
}

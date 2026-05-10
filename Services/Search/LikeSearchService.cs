using bestgen.Data;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services.Search;

/// <summary>
/// LIKE-based search across the most-searched entities. Works on both SQLite
/// and Postgres without extensions. Queries are tenant-scoped via the
/// existing EF query filter.
/// </summary>
public class LikeSearchService : ISearchService
{
    private const int PerEntity = 8;

    private readonly ApplicationDbContext _db;
    public LikeSearchService(ApplicationDbContext db) { _db = db; }

    public async Task<SearchResults> SearchAsync(string query, CancellationToken ct = default)
    {
        var q = (query ?? "").Trim();
        if (q.Length == 0)
        {
            return new SearchResults("", new Dictionary<string, IReadOnlyList<SearchHit>>(), 0);
        }
        var like = $"%{q}%";

        var customers = await _db.Customers.AsNoTracking()
            .Where(c => EF.Functions.Like(c.NameAr, like)
                     || (c.NameEn != null && EF.Functions.Like(c.NameEn, like))
                     || EF.Functions.Like(c.CustomerCode, like)
                     || (c.VatNumber != null && EF.Functions.Like(c.VatNumber, like))
                     || (c.Phone != null && EF.Functions.Like(c.Phone, like))
                     || (c.Email != null && EF.Functions.Like(c.Email, like)))
            .OrderBy(c => c.NameAr)
            .Take(PerEntity)
            .Select(c => new SearchHit("Customer", c.Id,
                c.NameEn ?? c.NameAr,
                $"{c.CustomerCode} · {c.Phone ?? c.Email ?? ""}",
                $"/Customers/Details/{c.Id}"))
            .ToListAsync(ct);

        var suppliers = await _db.Suppliers.AsNoTracking()
            .Where(s => EF.Functions.Like(s.NameAr, like)
                     || (s.NameEn != null && EF.Functions.Like(s.NameEn, like))
                     || EF.Functions.Like(s.SupplierCode, like)
                     || (s.VatNumber != null && EF.Functions.Like(s.VatNumber, like))
                     || (s.Phone != null && EF.Functions.Like(s.Phone, like)))
            .OrderBy(s => s.NameAr)
            .Take(PerEntity)
            .Select(s => new SearchHit("Supplier", s.Id,
                s.NameEn ?? s.NameAr,
                $"{s.SupplierCode} · {s.Phone ?? s.Email ?? ""}",
                $"/Suppliers/Details/{s.Id}"))
            .ToListAsync(ct);

        var products = await _db.Products.AsNoTracking()
            .Where(p => EF.Functions.Like(p.NameAr, like)
                     || (p.NameEn != null && EF.Functions.Like(p.NameEn, like))
                     || EF.Functions.Like(p.SKU, like)
                     || (p.Barcode != null && EF.Functions.Like(p.Barcode, like)))
            .OrderBy(p => p.NameAr)
            .Take(PerEntity)
            .Select(p => new SearchHit("Product", p.Id,
                p.NameEn ?? p.NameAr,
                $"{p.SKU} · {p.SellingPrice:N2} SAR",
                $"/Products/Details/{p.Id}"))
            .ToListAsync(ct);

        var invoices = await _db.SalesInvoices.AsNoTracking()
            .Include(i => i.Customer)
            .Where(i => EF.Functions.Like(i.InvoiceNumber, like)
                     || (i.Notes != null && EF.Functions.Like(i.Notes, like))
                     || (i.Customer != null && EF.Functions.Like(i.Customer.NameAr, like)))
            .OrderByDescending(i => i.InvoiceDate)
            .Take(PerEntity)
            .Select(i => new SearchHit("SalesInvoice", i.Id,
                i.InvoiceNumber,
                $"{i.InvoiceDate:yyyy-MM-dd} · {(i.Customer != null ? i.Customer.NameAr : "—")} · {i.GrandTotal:N2} SAR · {i.Status}",
                $"/SalesInvoices/Details/{i.Id}"))
            .ToListAsync(ct);

        var purchases = await _db.PurchaseInvoices.AsNoTracking()
            .Include(i => i.Supplier)
            .Where(i => EF.Functions.Like(i.PurchaseInvoiceNumber, like)
                     || (i.Notes != null && EF.Functions.Like(i.Notes, like))
                     || (i.SupplierInvoiceReference != null && EF.Functions.Like(i.SupplierInvoiceReference, like))
                     || (i.Supplier != null && EF.Functions.Like(i.Supplier.NameAr, like)))
            .OrderByDescending(i => i.InvoiceDate)
            .Take(PerEntity)
            .Select(i => new SearchHit("PurchaseInvoice", i.Id,
                i.PurchaseInvoiceNumber,
                $"{i.InvoiceDate:yyyy-MM-dd} · {(i.Supplier != null ? i.Supplier.NameAr : "—")} · {i.GrandTotal:N2} SAR · {i.Status}",
                $"/PurchaseInvoices/Details/{i.Id}"))
            .ToListAsync(ct);

        var quotations = await _db.SalesQuotations.AsNoTracking()
            .Include(x => x.Customer)
            .Where(x => EF.Functions.Like(x.QuotationNumber, like)
                     || (x.Notes != null && EF.Functions.Like(x.Notes, like))
                     || (x.Customer != null && EF.Functions.Like(x.Customer.NameAr, like)))
            .OrderByDescending(x => x.QuotationDate)
            .Take(PerEntity)
            .Select(x => new SearchHit("Quotation", x.Id,
                x.QuotationNumber,
                $"{x.QuotationDate:yyyy-MM-dd} · {(x.Customer != null ? x.Customer.NameAr : "—")} · {x.GrandTotal:N2} SAR · {x.Status}",
                $"/SalesQuotations/Details/{x.Id}"))
            .ToListAsync(ct);

        var byType = new Dictionary<string, IReadOnlyList<SearchHit>>(StringComparer.OrdinalIgnoreCase);
        if (customers.Count > 0)  byType["Customer"] = customers;
        if (suppliers.Count > 0)  byType["Supplier"] = suppliers;
        if (products.Count > 0)   byType["Product"] = products;
        if (invoices.Count > 0)   byType["SalesInvoice"] = invoices;
        if (purchases.Count > 0)  byType["PurchaseInvoice"] = purchases;
        if (quotations.Count > 0) byType["Quotation"] = quotations;

        var total = byType.Values.Sum(v => v.Count);
        return new SearchResults(q, byType, total);
    }
}

using System.Text;
using System.Text.Json;
using bestgen.Data;
using bestgen.Models;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services.Import;

/// <summary>
/// Generic CSV importer for the most-imported entities (Customer, Supplier,
/// Product). Auto-maps columns by header name using EN+AR synonyms; the user
/// can override before commit. Idempotent — skips rows whose code/SKU
/// already exists in the tenant.
/// </summary>
public class CsvImportService
{
    private readonly ApplicationDbContext _db;
    public CsvImportService(ApplicationDbContext db) { _db = db; }

    // Synonym table per entity field. First match wins.
    private static readonly Dictionary<ImportEntity, Dictionary<string, string[]>> FieldSynonyms = new()
    {
        [ImportEntity.Customer] = new()
        {
            ["CustomerCode"]                 = new[] { "customer code", "code", "id", "رمز", "كود" },
            ["NameAr"]                       = new[] { "name", "customer name", "client", "اسم العميل", "اسم", "الاسم" },
            ["NameEn"]                       = new[] { "name en", "english name", "name (en)", "الاسم بالإنجليزي" },
            ["VatNumber"]                    = new[] { "vat", "vat number", "tax id", "رقم ضريبي", "الرقم الضريبي" },
            ["CommercialRegistrationNumber"] = new[] { "cr", "commercial registration", "السجل التجاري", "س.ت" },
            ["Phone"]                        = new[] { "phone", "mobile", "telephone", "tel", "هاتف", "جوال" },
            ["Email"]                        = new[] { "email", "e-mail", "بريد", "البريد الإلكتروني" },
            ["City"]                         = new[] { "city", "town", "المدينة" },
            ["Address"]                      = new[] { "address", "العنوان", "عنوان" },
            ["OpeningBalance"]               = new[] { "opening balance", "balance", "رصيد افتتاحي" },
        },
        [ImportEntity.Supplier] = new()
        {
            ["SupplierCode"]                 = new[] { "supplier code", "code", "id", "رمز المورد", "كود" },
            ["NameAr"]                       = new[] { "name", "supplier name", "vendor", "اسم المورد", "اسم", "الاسم" },
            ["NameEn"]                       = new[] { "name en", "english name", "الاسم بالإنجليزي" },
            ["VatNumber"]                    = new[] { "vat", "vat number", "tax id", "رقم ضريبي" },
            ["CommercialRegistrationNumber"] = new[] { "cr", "commercial registration", "السجل التجاري" },
            ["Phone"]                        = new[] { "phone", "mobile", "tel", "هاتف", "جوال" },
            ["Email"]                        = new[] { "email", "e-mail", "بريد", "البريد الإلكتروني" },
            ["Address"]                      = new[] { "address", "العنوان" },
            ["OpeningBalance"]               = new[] { "opening balance", "balance", "رصيد افتتاحي" },
        },
        [ImportEntity.Product] = new()
        {
            ["SKU"]                          = new[] { "sku", "code", "item code", "رمز", "كود الصنف" },
            ["Barcode"]                      = new[] { "barcode", "ean", "upc", "الباركود" },
            ["NameAr"]                       = new[] { "name", "product name", "اسم الصنف", "اسم" },
            ["NameEn"]                       = new[] { "name en", "english name", "الاسم بالإنجليزي" },
            ["Category"]                     = new[] { "category", "التصنيف", "الفئة" },
            ["Unit"]                         = new[] { "unit", "uom", "الوحدة" },
            ["PurchasePrice"]                = new[] { "purchase price", "cost", "سعر الشراء", "التكلفة" },
            ["SellingPrice"]                 = new[] { "selling price", "price", "سعر البيع", "السعر" },
            ["VatRate"]                      = new[] { "vat", "vat rate", "tax rate", "نسبة الضريبة" },
            ["OpeningStock"]                 = new[] { "opening stock", "stock", "qty", "الكمية", "المخزون" },
        }
    };

    /// <summary>Parse + auto-map. Returns a preview the wizard renders.</summary>
    public ImportPreview Preview(ImportEntity entity, string fileName, string csv)
    {
        var rows = ParseCsv(csv);
        if (rows.Count < 2)
        {
            return new ImportPreview(entity, fileName, Array.Empty<string>(), Array.Empty<string?>(),
                Array.Empty<IReadOnlyList<string>>(), 0, "CSV must have a header row + at least one data row.");
        }
        var headers = rows[0];
        var data = rows.Skip(1).Take(10).ToList();
        var mapping = AutoMap(entity, headers);
        return new ImportPreview(entity, fileName, headers, mapping, data, rows.Count - 1, null);
    }

    public async Task<ImportJob> CommitAsync(ImportEntity entity, string fileName, string csv,
        IReadOnlyList<string?> mapping, string? userId, CancellationToken ct = default)
    {
        var job = new ImportJob
        {
            Entity = entity,
            FileName = fileName,
            ColumnMappingJson = JsonSerializer.Serialize(mapping),
            CreatedByUserId = userId,
            Status = ImportStatus.Pending
        };
        _db.ImportJobs.Add(job);
        await _db.SaveChangesAsync(ct);

        try
        {
            var rows = ParseCsv(csv);
            if (rows.Count < 2) throw new InvalidOperationException("CSV needs header + data rows.");
            var headers = rows[0];

            // Build header→fieldIndex lookup so the row reader knows which CSV column maps to which entity field.
            // mapping[i] is the entity field name for header i (or null/skip).
            string? get(IReadOnlyList<string> row, string field)
            {
                for (var i = 0; i < headers.Count && i < mapping.Count; i++)
                {
                    if (string.Equals(mapping[i], field, StringComparison.OrdinalIgnoreCase))
                    {
                        return i < row.Count ? row[i] : null;
                    }
                }
                return null;
            }

            var errors = new List<string>();
            for (var r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                if (row.Count == 0 || row.All(string.IsNullOrWhiteSpace)) continue;
                job.TotalRows++;
                try
                {
                    var imported = await ImportRowAsync(entity, row, get, ct);
                    if (imported) job.ImportedRows++; else job.SkippedRows++;
                }
                catch (Exception ex)
                {
                    job.FailedRows++;
                    errors.Add($"Row {r + 1}: {ex.Message}");
                    if (errors.Count > 30) break; // cap error log
                }
            }
            await _db.SaveChangesAsync(ct);

            job.Status = ImportStatus.Completed;
            job.CompletedAt = DateTime.UtcNow;
            if (errors.Count > 0) job.ErrorSummary = string.Join("\n", errors);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            job.Status = ImportStatus.Failed;
            job.ErrorSummary = ex.Message;
            await _db.SaveChangesAsync(ct);
        }
        return job;
    }

    private async Task<bool> ImportRowAsync(
        ImportEntity entity, IReadOnlyList<string> row,
        Func<IReadOnlyList<string>, string, string?> get,
        CancellationToken ct)
    {
        decimal parseDec(string? s) => decimal.TryParse((s ?? "").Replace(",", "").Trim(),
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0m;

        switch (entity)
        {
            case ImportEntity.Customer:
            {
                var nameAr = (get(row, "NameAr") ?? get(row, "NameEn") ?? "").Trim();
                if (string.IsNullOrEmpty(nameAr)) return false;
                var code = (get(row, "CustomerCode") ?? "").Trim();
                if (string.IsNullOrEmpty(code))
                {
                    code = $"IMP-{Math.Abs(nameAr.GetHashCode()):X6}";
                }
                var dup = await _db.Customers.AnyAsync(c => c.CustomerCode == code || c.NameAr == nameAr, ct);
                if (dup) return false;
                _db.Customers.Add(new Customer
                {
                    CustomerCode = code,
                    NameAr = nameAr,
                    NameEn = (get(row, "NameEn") ?? "").Trim() is var ne && string.IsNullOrEmpty(ne) ? null : ne,
                    VatNumber = (get(row, "VatNumber") ?? "").Trim() is var vat && string.IsNullOrEmpty(vat) ? null : vat,
                    CommercialRegistrationNumber = (get(row, "CommercialRegistrationNumber") ?? "").Trim() is var cr && string.IsNullOrEmpty(cr) ? null : cr,
                    Phone = (get(row, "Phone") ?? "").Trim() is var ph && string.IsNullOrEmpty(ph) ? null : ph,
                    Email = (get(row, "Email") ?? "").Trim() is var em && string.IsNullOrEmpty(em) ? null : em,
                    City = (get(row, "City") ?? "").Trim() is var ci && string.IsNullOrEmpty(ci) ? null : ci,
                    Address = (get(row, "Address") ?? "").Trim() is var ad && string.IsNullOrEmpty(ad) ? null : ad,
                    OpeningBalance = parseDec(get(row, "OpeningBalance")),
                    CurrentBalance = parseDec(get(row, "OpeningBalance"))
                });
                return true;
            }
            case ImportEntity.Supplier:
            {
                var nameAr = (get(row, "NameAr") ?? get(row, "NameEn") ?? "").Trim();
                if (string.IsNullOrEmpty(nameAr)) return false;
                var code = (get(row, "SupplierCode") ?? "").Trim();
                if (string.IsNullOrEmpty(code))
                {
                    code = $"IMP-S-{Math.Abs(nameAr.GetHashCode()):X6}";
                }
                var dup = await _db.Suppliers.AnyAsync(s => s.SupplierCode == code || s.NameAr == nameAr, ct);
                if (dup) return false;
                _db.Suppliers.Add(new Supplier
                {
                    SupplierCode = code,
                    NameAr = nameAr,
                    NameEn = (get(row, "NameEn") ?? "").Trim() is var ne && string.IsNullOrEmpty(ne) ? null : ne,
                    VatNumber = (get(row, "VatNumber") ?? "").Trim() is var vat && string.IsNullOrEmpty(vat) ? null : vat,
                    CommercialRegistrationNumber = (get(row, "CommercialRegistrationNumber") ?? "").Trim() is var cr && string.IsNullOrEmpty(cr) ? null : cr,
                    Phone = (get(row, "Phone") ?? "").Trim() is var ph && string.IsNullOrEmpty(ph) ? null : ph,
                    Email = (get(row, "Email") ?? "").Trim() is var em && string.IsNullOrEmpty(em) ? null : em,
                    Address = (get(row, "Address") ?? "").Trim() is var ad && string.IsNullOrEmpty(ad) ? null : ad,
                    OpeningBalance = parseDec(get(row, "OpeningBalance")),
                    CurrentBalance = parseDec(get(row, "OpeningBalance"))
                });
                return true;
            }
            case ImportEntity.Product:
            {
                var nameAr = (get(row, "NameAr") ?? get(row, "NameEn") ?? "").Trim();
                if (string.IsNullOrEmpty(nameAr)) return false;
                var sku = (get(row, "SKU") ?? "").Trim();
                if (string.IsNullOrEmpty(sku))
                {
                    sku = $"IMP-P-{Math.Abs(nameAr.GetHashCode()):X6}";
                }
                if (await _db.Products.AnyAsync(p => p.SKU == sku, ct)) return false;
                _db.Products.Add(new Product
                {
                    SKU = sku,
                    Barcode = (get(row, "Barcode") ?? "").Trim() is var bc && string.IsNullOrEmpty(bc) ? null : bc,
                    NameAr = nameAr,
                    NameEn = (get(row, "NameEn") ?? "").Trim() is var ne && string.IsNullOrEmpty(ne) ? null : ne,
                    Category = (get(row, "Category") ?? "").Trim() is var ca && string.IsNullOrEmpty(ca) ? null : ca,
                    Unit = (get(row, "Unit") ?? "").Trim() is var u && string.IsNullOrEmpty(u) ? "قطعة" : u,
                    PurchasePrice = parseDec(get(row, "PurchasePrice")),
                    SellingPrice = parseDec(get(row, "SellingPrice")),
                    VatRate = (parseDec(get(row, "VatRate")) is var vr && vr > 0) ? vr : 15m,
                    OpeningStock = parseDec(get(row, "OpeningStock")),
                    CurrentStock = parseDec(get(row, "OpeningStock"))
                });
                return true;
            }
        }
        return false;
    }

    public IReadOnlyList<string> EntityFields(ImportEntity entity) =>
        FieldSynonyms[entity].Keys.ToList();

    private static IReadOnlyList<string?> AutoMap(ImportEntity entity, IReadOnlyList<string> headers)
    {
        var syn = FieldSynonyms[entity];
        var mapping = new string?[headers.Count];
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Count; i++)
        {
            var h = headers[i].Trim().ToLowerInvariant();
            foreach (var (field, names) in syn)
            {
                if (taken.Contains(field)) continue;
                if (names.Any(n => string.Equals(h, n, StringComparison.OrdinalIgnoreCase))
                    || names.Any(n => h.Contains(n, StringComparison.OrdinalIgnoreCase)))
                {
                    mapping[i] = field;
                    taken.Add(field);
                    break;
                }
            }
        }
        return mapping;
    }

    // ---------- CSV parser (RFC 4180-ish) ----------
    public static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        if (string.IsNullOrEmpty(text)) return rows;
        var current = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = false;
                }
                else sb.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',') { current.Add(sb.ToString()); sb.Clear(); }
                else if (c == '\r') { /* skip */ }
                else if (c == '\n')
                {
                    current.Add(sb.ToString()); sb.Clear();
                    rows.Add(current); current = new List<string>();
                }
                else sb.Append(c);
            }
            i++;
        }
        if (sb.Length > 0 || current.Count > 0) { current.Add(sb.ToString()); rows.Add(current); }
        // Trim purely-empty trailing rows
        while (rows.Count > 0 && rows[^1].All(string.IsNullOrWhiteSpace)) rows.RemoveAt(rows.Count - 1);
        return rows;
    }
}

public sealed record ImportPreview(
    ImportEntity Entity,
    string FileName,
    IReadOnlyList<string> Headers,
    IReadOnlyList<string?> Mapping,
    IReadOnlyList<IReadOnlyList<string>> SampleRows,
    int TotalDataRows,
    string? Error);

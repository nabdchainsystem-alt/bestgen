using bestgen.Data;
using bestgen.Models;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services.Onboarding;

public enum IndustryTemplate
{
    Restaurant,
    Trading,
    Services,
    Contractor
}

public sealed record TemplateApplyResult(int CategoriesAdded, int ProductsAdded, int AccountsAdded);

public sealed record TemplateInfo(
    IndustryTemplate Template,
    string Icon,
    string TitleEn, string TitleAr,
    string SummaryEn, string SummaryAr,
    string[] BulletsEn, string[] BulletsAr);

/// <summary>
/// Applies industry-specific seed data (categories, sample products, COGS accounts)
/// to the current tenant. Idempotent — uses NameAr / SKU / AccountCode as keys
/// and skips existing rows so re-applying is safe.
/// </summary>
public class IndustryTemplateService
{
    private readonly ApplicationDbContext _db;

    public IndustryTemplateService(ApplicationDbContext db) { _db = db; }

    public IReadOnlyList<TemplateInfo> Catalog { get; } = new[]
    {
        new TemplateInfo(IndustryTemplate.Restaurant,
            "bi-cup-hot",
            "Restaurant / Café", "مطعم / كافيه",
            "Pre-loaded menu and kitchen cost accounts.",
            "قائمة طعام وحسابات تكلفة المطبخ جاهزة.",
            new[] { "6 menu categories (Appetizers, Mains, Desserts, Beverages, Coffee, Juices)",
                    "10 sample menu items with prices in SAR",
                    "Food cost / Beverage cost / Kitchen wages accounts" },
            new[] { "6 أقسام للقائمة (المقبلات، الأطباق الرئيسية، الحلويات، المشروبات، القهوة، العصائر)",
                    "10 أصناف نموذجية بأسعار بالريال",
                    "حسابات تكلفة الطعام والمشروبات وأجور المطبخ" }),

        new TemplateInfo(IndustryTemplate.Trading,
            "bi-bag",
            "Trading / Retail", "تجارة وتجزئة",
            "General-trade categories and a Cost-of-Goods-Sold chart.",
            "تصنيفات تجارة عامة ومخطط تكلفة البضاعة المباعة.",
            new[] { "5 product categories (Groceries, Electronics, Clothing, Home Goods, Office)",
                    "10 sample products across the categories",
                    "COGS, Sales Returns, Quantity Discount accounts" },
            new[] { "5 تصنيفات منتجات (مواد غذائية، إلكترونيات، ملابس، أدوات منزلية، مكتبية)",
                    "10 منتجات نموذجية متنوعة",
                    "حسابات تكلفة البضاعة المباعة، مرتجعات، خصم الكمية" }),

        new TemplateInfo(IndustryTemplate.Services,
            "bi-briefcase",
            "Professional Services", "خدمات مهنية",
            "Hourly billing, retainer items, and service-revenue accounts.",
            "بنود فوترة بالساعة، اشتراكات، وحسابات إيرادات الخدمات.",
            new[] { "5 service categories (Consulting, Maintenance, Training, Design, Tech Support)",
                    "5 service items (priced per hour / visit / session)",
                    "Consulting and Maintenance revenue accounts + technician wages" },
            new[] { "5 تصنيفات خدمات (استشارات، صيانة، تدريب، تصميم، دعم فني)",
                    "5 بنود خدمة (بالساعة / الزيارة / الجلسة)",
                    "حسابات إيرادات الاستشارات والصيانة وأجور الفنيين" }),

        new TemplateInfo(IndustryTemplate.Contractor,
            "bi-bricks",
            "Contractor / Construction", "مقاولات وبناء",
            "Construction-materials catalog and project cost accounts.",
            "كتالوج مواد بناء وحسابات تكلفة المشاريع.",
            new[] { "4 categories (Materials, Equipment, Labor, Subcontractors)",
                    "10 construction materials (cement, rebar, sand, paint, tiles, ...)",
                    "Material / Labor / Equipment / Subcontractor cost accounts" },
            new[] { "4 تصنيفات (مواد، معدات، عمالة، مقاولون فرعيون)",
                    "10 مواد بناء (إسمنت، حديد، رمل، دهان، بلاط، …)",
                    "حسابات تكلفة المواد والعمالة والمعدات والمقاولين" }),
    };

    public async Task<TemplateApplyResult> ApplyAsync(IndustryTemplate template, CancellationToken ct = default)
    {
        var data = BuildSeed(template);

        // ProductCategories — keyed by NameAr (per tenant via query filter).
        var existingCategoryNames = await _db.ProductCategories
            .Select(c => c.NameAr).ToListAsync(ct);
        var existingCategorySet = new HashSet<string>(existingCategoryNames, StringComparer.OrdinalIgnoreCase);

        var addedCategoryNames = new List<string>();
        foreach (var c in data.Categories)
        {
            if (existingCategorySet.Contains(c.NameAr)) continue;
            _db.ProductCategories.Add(new ProductCategory
            {
                NameAr = c.NameAr,
                NameEn = c.NameEn,
                IsActive = true
            });
            addedCategoryNames.Add(c.NameAr);
        }
        if (addedCategoryNames.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        // Re-read categories with their generated ids so we can attach products.
        var categoryByName = await _db.ProductCategories
            .ToDictionaryAsync(c => c.NameAr, c => c.Id, ct);

        // Products — keyed by SKU.
        var existingSkus = await _db.Products.Select(p => p.SKU).ToListAsync(ct);
        var existingSkuSet = new HashSet<string>(existingSkus, StringComparer.OrdinalIgnoreCase);

        var productsAdded = 0;
        foreach (var p in data.Products)
        {
            if (existingSkuSet.Contains(p.SKU)) continue;
            int? catId = null;
            if (!string.IsNullOrEmpty(p.CategoryNameAr) && categoryByName.TryGetValue(p.CategoryNameAr, out var id))
            {
                catId = id;
            }
            _db.Products.Add(new Product
            {
                SKU = p.SKU,
                NameAr = p.NameAr,
                NameEn = p.NameEn,
                Category = p.CategoryNameAr,
                CategoryId = catId,
                Unit = p.Unit,
                PurchasePrice = p.PurchasePrice,
                SellingPrice = p.SellingPrice,
                VatRate = 15
            });
            productsAdded++;
        }

        // Accounts — keyed by AccountCode.
        var existingCodes = await _db.Accounts.Select(a => a.AccountCode).ToListAsync(ct);
        var existingCodeSet = new HashSet<string>(existingCodes, StringComparer.OrdinalIgnoreCase);

        var accountsAdded = 0;
        foreach (var a in data.Accounts)
        {
            if (existingCodeSet.Contains(a.Code)) continue;
            _db.Accounts.Add(new Account
            {
                AccountCode = a.Code,
                AccountNameAr = a.NameAr,
                AccountNameEn = a.NameEn,
                AccountType = a.Type,
                IsActive = true
            });
            accountsAdded++;
        }

        if (productsAdded > 0 || accountsAdded > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        return new TemplateApplyResult(addedCategoryNames.Count, productsAdded, accountsAdded);
    }

    // ---------- Seed data ----------
    private sealed record CatRow(string NameAr, string? NameEn);
    private sealed record ProdRow(string SKU, string NameAr, string? NameEn, string CategoryNameAr, string Unit, decimal PurchasePrice, decimal SellingPrice);
    private sealed record AcctRow(string Code, string NameAr, string? NameEn, AccountType Type);
    private sealed record SeedSet(IReadOnlyList<CatRow> Categories, IReadOnlyList<ProdRow> Products, IReadOnlyList<AcctRow> Accounts);

    private SeedSet BuildSeed(IndustryTemplate template) => template switch
    {
        IndustryTemplate.Restaurant => new SeedSet(
            new CatRow[] {
                new("المقبلات",          "Appetizers"),
                new("الأطباق الرئيسية",  "Main Courses"),
                new("الحلويات",          "Desserts"),
                new("المشروبات",         "Beverages"),
                new("القهوة",            "Coffee"),
                new("العصائر",           "Juices"),
            },
            new ProdRow[] {
                new("TPL-RST-001", "شاي",          "Tea",                "المشروبات",        "كوب",  1.5m,  5m),
                new("TPL-RST-002", "قهوة عربية",   "Arabic Coffee",      "القهوة",           "كوب",  3m,    8m),
                new("TPL-RST-003", "كابتشينو",     "Cappuccino",         "القهوة",           "كوب",  6m,   15m),
                new("TPL-RST-004", "لاتيه",        "Latte",              "القهوة",           "كوب",  7m,   18m),
                new("TPL-RST-005", "برغر دجاج",    "Chicken Burger",     "الأطباق الرئيسية", "طبق", 14m,  35m),
                new("TPL-RST-006", "برغر لحم",     "Beef Burger",        "الأطباق الرئيسية", "طبق", 18m,  45m),
                new("TPL-RST-007", "بيتزا مارغريتا","Margherita Pizza",  "الأطباق الرئيسية", "طبق", 20m,  50m),
                new("TPL-RST-008", "سلطة سيزر",    "Caesar Salad",       "المقبلات",         "طبق", 12m,  30m),
                new("TPL-RST-009", "تشيز كيك",     "Cheesecake",         "الحلويات",         "قطعة",10m,  25m),
                new("TPL-RST-010", "آيس كريم",     "Ice Cream",          "الحلويات",         "كوب",  8m,   20m),
            },
            new AcctRow[] {
                new("5101", "تكلفة الطعام",     "Food Cost",        AccountType.Expense),
                new("5102", "تكلفة المشروبات",  "Beverage Cost",    AccountType.Expense),
                new("5103", "أجور المطبخ",       "Kitchen Wages",    AccountType.Expense),
            }),

        IndustryTemplate.Trading => new SeedSet(
            new CatRow[] {
                new("مواد غذائية",      "Groceries"),
                new("إلكترونيات",       "Electronics"),
                new("ملابس",            "Clothing"),
                new("أدوات منزلية",     "Home Goods"),
                new("مستلزمات مكتبية",  "Office Supplies"),
            },
            new ProdRow[] {
                new("TPL-TRD-001", "أرز بسمتي 5 كجم",  "Basmati Rice 5kg",  "مواد غذائية",      "كيس",  35m,  55m),
                new("TPL-TRD-002", "زيت زيتون 1 لتر",  "Olive Oil 1L",      "مواد غذائية",      "زجاجة",22m,  38m),
                new("TPL-TRD-003", "سماعة بلوتوث",     "Bluetooth Headset", "إلكترونيات",       "قطعة", 60m, 145m),
                new("TPL-TRD-004", "شاحن سريع 30 واط", "Fast Charger 30W",  "إلكترونيات",       "قطعة", 35m,  79m),
                new("TPL-TRD-005", "قميص قطني",        "Cotton Shirt",      "ملابس",            "قطعة", 45m, 120m),
                new("TPL-TRD-006", "بنطلون جينز",      "Denim Jeans",       "ملابس",            "قطعة", 80m, 195m),
                new("TPL-TRD-007", "مقلاة 28 سم",      "Frying Pan 28cm",   "أدوات منزلية",     "قطعة", 55m, 119m),
                new("TPL-TRD-008", "طقم سكاكين",       "Knife Set",         "أدوات منزلية",     "طقم",  90m, 175m),
                new("TPL-TRD-009", "رزمة ورق A4",      "A4 Paper Ream",     "مستلزمات مكتبية",  "رزمة", 12m,  22m),
                new("TPL-TRD-010", "قلم حبر جاف",      "Ballpoint Pen",     "مستلزمات مكتبية",  "قطعة",  1m,    3m),
            },
            new AcctRow[] {
                new("5201", "تكلفة البضاعة المباعة", "Cost of Goods Sold", AccountType.Expense),
                new("5202", "مرتجعات المبيعات",      "Sales Returns",      AccountType.Revenue),
                new("5203", "خصم الكمية",            "Quantity Discount",  AccountType.Expense),
            }),

        IndustryTemplate.Services => new SeedSet(
            new CatRow[] {
                new("استشارات",   "Consulting"),
                new("صيانة",      "Maintenance"),
                new("تدريب",      "Training"),
                new("تصميم",      "Design"),
                new("دعم فني",    "Technical Support"),
            },
            new ProdRow[] {
                new("TPL-SRV-001", "ساعة استشارية",      "Consulting Hour",       "استشارات", "ساعة",   0m,  250m),
                new("TPL-SRV-002", "زيارة صيانة",        "Maintenance Visit",     "صيانة",    "زيارة",  0m,  350m),
                new("TPL-SRV-003", "جلسة تدريب",         "Training Session",      "تدريب",    "جلسة",   0m,  500m),
                new("TPL-SRV-004", "تصميم شعار",         "Logo Design",           "تصميم",    "تصميم",  0m, 1500m),
                new("TPL-SRV-005", "صيانة شهرية",        "Monthly Maintenance",   "صيانة",    "شهر",    0m,  800m),
            },
            new AcctRow[] {
                new("4201", "إيرادات الاستشارات", "Consulting Revenue",   AccountType.Revenue),
                new("4202", "إيرادات الصيانة",   "Maintenance Revenue",  AccountType.Revenue),
                new("5301", "أجور الفنيين",       "Technician Wages",    AccountType.Expense),
            }),

        IndustryTemplate.Contractor => new SeedSet(
            new CatRow[] {
                new("مواد البناء",       "Construction Materials"),
                new("معدات",             "Equipment"),
                new("عمالة",             "Labor"),
                new("مقاولين فرعيين",    "Subcontractors"),
            },
            new ProdRow[] {
                new("TPL-CON-001", "إسمنت 50 كجم",       "Cement 50kg",          "مواد البناء", "كيس", 18m,  28m),
                new("TPL-CON-002", "حديد تسليح 12 مم",   "Rebar 12mm",           "مواد البناء", "متر",  6m,  10m),
                new("TPL-CON-003", "رمل مغسول",          "Washed Sand",          "مواد البناء", "متر مكعب", 35m, 60m),
                new("TPL-CON-004", "بلاط بورسلين",       "Porcelain Tiles",      "مواد البناء", "متر مربع", 30m, 65m),
                new("TPL-CON-005", "دهان داخلي 18 لتر",  "Interior Paint 18L",   "مواد البناء", "علبة", 90m, 175m),
                new("TPL-CON-006", "خلاطة إسمنت",        "Cement Mixer",         "معدات",       "وحدة",1500m, 0m),
                new("TPL-CON-007", "سقالة معدنية",        "Steel Scaffold",       "معدات",       "وحدة", 800m, 0m),
                new("TPL-CON-008", "ساعة عامل بناء",     "Construction Labor Hr","عمالة",       "ساعة",  20m,  35m),
                new("TPL-CON-009", "ساعة كهربائي",       "Electrician Hour",     "عمالة",       "ساعة",  35m,  60m),
                new("TPL-CON-010", "ساعة سباك",          "Plumber Hour",         "عمالة",       "ساعة",  30m,  55m),
            },
            new AcctRow[] {
                new("5401", "تكاليف المواد",        "Material Costs",         AccountType.Expense),
                new("5402", "أجور العمال",          "Labor Costs",            AccountType.Expense),
                new("5403", "تكاليف المعدات",       "Equipment Costs",        AccountType.Expense),
                new("5404", "مقاولين فرعيين",       "Subcontractor Costs",    AccountType.Expense),
            }),

        _ => throw new ArgumentOutOfRangeException(nameof(template), template, null)
    };
}

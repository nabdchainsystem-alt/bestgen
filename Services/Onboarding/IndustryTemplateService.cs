using bestgen.Data;
using bestgen.Models;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services.Onboarding;

public enum IndustryTemplate
{
    Restaurant,
    Cafe,
    Bakery,
    Trading,
    Pharmacy,
    Clinic,
    Salon,
    Gym,
    AutoRepair,
    Printing,
    EducationCenter,
    Hotel,
    RealEstate,
    Services,
    Contractor,
    EcommerceStore
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
        new TemplateInfo(IndustryTemplate.Restaurant,    "bi-cup-hot",
            "Restaurant",       "مطعم",
            "Full-menu restaurant with kitchen + service cost accounts.",
            "مطعم بقائمة طعام كاملة مع حسابات تكلفة المطبخ والخدمة.",
            new[] { "8 menu sections (Appetizers, Mains, Pasta, Pizza, Desserts, Beverages, Salads, Sides)",
                    "30 menu items across all sections",
                    "Kitchen / Beverage / Service / Delivery cost accounts" },
            new[] { "8 أقسام للقائمة (المقبلات، الأطباق الرئيسية، المعكرونة، البيتزا، الحلويات، المشروبات، السلطات، المقبلات الجانبية)",
                    "30 صنفاً متنوعاً", "حسابات تكلفة المطبخ والمشروبات والخدمة والتوصيل" }),

        new TemplateInfo(IndustryTemplate.Cafe,          "bi-cup-straw",
            "Café / Coffee Shop","كافيه ومحل قهوة",
            "Coffee + tea + light food menu, barista-friendly.",
            "كافيه بقائمة قهوة وشاي ومأكولات خفيفة.",
            new[] { "6 categories (Espresso, Brewed, Cold drinks, Specialty, Tea, Bakery)",
                    "25 drink + bakery items",
                    "Coffee bean / Milk / Pastry / Barista wages accounts" },
            new[] { "6 تصنيفات (إسبريسو، قهوة مفلترة، باردة، مميزة، شاي، مخبوزات)",
                    "25 صنف مشروبات ومخبوزات", "حسابات حبوب القهوة والحليب والمعجنات وأجور الباريستا" }),

        new TemplateInfo(IndustryTemplate.Bakery,        "bi-egg-fried",
            "Bakery / Pastry",   "مخبزة وحلويات",
            "Bread, pastries, cakes — including ingredient cost accounts.",
            "مخبزة بمنتجات الخبز والمعجنات والكيك مع حسابات المكونات.",
            new[] { "5 categories (Bread, Pastries, Cakes, Cookies, Custom orders)",
                    "20 baked goods at typical prices",
                    "Flour / Sugar / Dairy / Packaging cost accounts" },
            new[] { "5 تصنيفات (الخبز، المعجنات، الكيك، الكوكيز، الطلبات الخاصة)",
                    "20 منتج مخبوز بالأسعار الشائعة", "حسابات الدقيق والسكر ومنتجات الألبان والتغليف" }),

        new TemplateInfo(IndustryTemplate.Trading,       "bi-bag",
            "Trading / Retail",  "تجارة وتجزئة",
            "General retail with broad product mix and a clean COGS chart.",
            "تجارة عامة بتشكيلة منتجات واسعة ومخطط تكلفة بضاعة منظم.",
            new[] { "8 categories (Groceries, Electronics, Clothing, Home, Office, Toys, Health, Beauty)",
                    "32 sample SKUs",
                    "COGS, Sales Returns, Quantity Discount, Freight In/Out" },
            new[] { "8 تصنيفات (مواد غذائية، إلكترونيات، ملابس، منزلية، مكتبية، ألعاب، صحة، تجميل)",
                    "32 صنف نموذجي", "حسابات تكلفة البضاعة، مرتجعات، خصم الكمية، شحن داخل/خارج" }),

        new TemplateInfo(IndustryTemplate.Pharmacy,      "bi-capsule",
            "Pharmacy",          "صيدلية",
            "OTC + prescription medicines with cold-storage tracking.",
            "صيدلية بأدوية بدون وصفة وموصوفة مع تتبع التبريد.",
            new[] { "6 categories (Pain relief, Cold & flu, Vitamins, Skincare, Baby, First-aid)",
                    "25 OTC medicine SKUs with typical KSA pricing",
                    "Pharmaceutical / Cold-chain / Returns to supplier accounts" },
            new[] { "6 تصنيفات (مسكنات، نزلات برد، فيتامينات، عناية بالبشرة، أطفال، إسعافات)",
                    "25 دواء OTC بالأسعار السعودية", "حسابات الأدوية والتبريد ومرتجعات الموردين" }),

        new TemplateInfo(IndustryTemplate.Clinic,        "bi-hospital",
            "Clinic / Medical",  "عيادة طبية",
            "Service-based medical clinic — consultations, procedures, follow-ups.",
            "عيادة طبية بخدمات الاستشارات والإجراءات والمتابعة.",
            new[] { "5 categories (General consult, Specialist, Lab, Procedures, Follow-up)",
                    "15 service items billed per visit",
                    "Medical fees / Lab costs / Insurance receivables accounts" },
            new[] { "5 تصنيفات (استشارة عامة، أخصائي، مختبر، إجراءات، متابعة)",
                    "15 خدمة طبية تُحاسب بالزيارة", "حسابات الأتعاب الطبية والمختبر وذمم التأمين" }),

        new TemplateInfo(IndustryTemplate.Salon,         "bi-scissors",
            "Salon / Beauty",    "صالون تجميل",
            "Hair, nails, skincare and cosmetics retail combined.",
            "صالون شعر وأظافر وعناية بالبشرة مع بيع منتجات التجميل.",
            new[] { "6 categories (Hair, Nails, Facials, Massage, Makeup, Retail)",
                    "20 service + product items",
                    "Stylist commission / Product COGS accounts" },
            new[] { "6 تصنيفات (شعر، أظافر، عناية بالوجه، مساج، مكياج، تجزئة)",
                    "20 خدمة ومنتج", "حسابات عمولات المصففين وتكلفة المنتجات" }),

        new TemplateInfo(IndustryTemplate.Gym,           "bi-bicycle",
            "Gym / Fitness",     "نادي رياضي",
            "Memberships, classes, personal training, supplements retail.",
            "اشتراكات ودورات وتدريب شخصي وبيع مكملات.",
            new[] { "5 categories (Memberships, Classes, PT sessions, Supplements, Apparel)",
                    "18 plan + service items",
                    "Trainer wages / Equipment maintenance / Supplement COGS" },
            new[] { "5 تصنيفات (اشتراكات، دورات، حصص شخصية، مكملات، ملابس)",
                    "18 خطة وخدمة", "حسابات أجور المدربين وصيانة المعدات وتكلفة المكملات" }),

        new TemplateInfo(IndustryTemplate.AutoRepair,    "bi-wrench-adjustable",
            "Auto Repair",       "ورشة سيارات",
            "Mechanical + electrical repair with parts inventory.",
            "ورشة ميكانيكا وكهرباء مع مخزون قطع الغيار.",
            new[] { "6 categories (Engine, Brakes, Suspension, Electrical, Body, Lubricants)",
                    "25 parts + labor SKUs",
                    "Spare parts COGS / Labor revenue / Warranty accounts" },
            new[] { "6 تصنيفات (محرك، فرامل، تعليق، كهرباء، صدامات، زيوت)",
                    "25 قطعة غيار وعمالة", "حسابات تكلفة قطع الغيار وإيرادات العمالة والضمان" }),

        new TemplateInfo(IndustryTemplate.Printing,      "bi-printer",
            "Printing / Stationery","مطبعة وقرطاسية",
            "Business printing + office stationery retail.",
            "مطبعة وقرطاسية بمنتجات وخدمات طباعة.",
            new[] { "5 categories (Business cards, Banners, Books, Office supplies, Custom)",
                    "20 print + stationery SKUs",
                    "Paper / Toner / Outsourced print costs" },
            new[] { "5 تصنيفات (بطاقات أعمال، لافتات، كتب، مكتبية، طلبات خاصة)",
                    "20 صنف طباعة وقرطاسية", "حسابات الورق والحبر والطباعة الخارجية" }),

        new TemplateInfo(IndustryTemplate.EducationCenter,"bi-mortarboard",
            "Education Center",  "مركز تعليمي",
            "Courses, certificates, tutoring — recurring billing friendly.",
            "دورات وشهادات ودروس خصوصية مع فوترة متكررة.",
            new[] { "5 categories (Languages, IT, Business, Kids, Certifications)",
                    "15 course offerings",
                    "Instructor fees / Certification / Materials accounts" },
            new[] { "5 تصنيفات (لغات، تقنية، أعمال، أطفال، شهادات)",
                    "15 دورة", "حسابات أتعاب المدربين والشهادات والمواد" }),

        new TemplateInfo(IndustryTemplate.Hotel,         "bi-building",
            "Hotel / Hospitality","فندق وضيافة",
            "Rooms, F&B, events. Day-rate + night-rate items.",
            "غرف ومأكولات وفعاليات. بنود سعر يومي وليلي.",
            new[] { "5 categories (Rooms, Restaurant, Events, Spa, Laundry)",
                    "18 room types + ancillary services",
                    "Housekeeping / Linen / Energy cost accounts" },
            new[] { "5 تصنيفات (غرف، مطعم، فعاليات، سبا، مغسلة)",
                    "18 نوع غرفة وخدمة مساعدة", "حسابات التدبير المنزلي والمفروشات والطاقة" }),

        new TemplateInfo(IndustryTemplate.RealEstate,    "bi-house-door",
            "Real Estate",       "عقارات",
            "Lease, rent collection, maintenance, agent commissions.",
            "إيجار وتحصيل وصيانة وعمولات وكلاء.",
            new[] { "5 categories (Residential lease, Commercial lease, Brokerage, Maintenance, Utilities)",
                    "12 service items priced per month / per deal",
                    "Rent income / Agent commission / Maintenance accounts" },
            new[] { "5 تصنيفات (إيجار سكني، إيجار تجاري، وساطة، صيانة، خدمات)",
                    "12 خدمة شهرية وعمولة", "حسابات إيراد الإيجار وعمولات الوكلاء والصيانة" }),

        new TemplateInfo(IndustryTemplate.Services,      "bi-briefcase",
            "Professional Services","خدمات مهنية",
            "Hourly billing, retainer items, and service-revenue accounts.",
            "بنود فوترة بالساعة، اشتراكات، وحسابات إيرادات الخدمات.",
            new[] { "6 categories (Consulting, Maintenance, Training, Design, Tech Support, Audit)",
                    "12 service items (per hour / visit / session)",
                    "Consulting and Maintenance revenue + technician wages" },
            new[] { "6 تصنيفات (استشارات، صيانة، تدريب، تصميم، دعم فني، تدقيق)",
                    "12 خدمة (بالساعة / الزيارة / الجلسة)",
                    "حسابات إيرادات الاستشارات والصيانة وأجور الفنيين" }),

        new TemplateInfo(IndustryTemplate.Contractor,    "bi-bricks",
            "Contractor",        "مقاولات وبناء",
            "Construction-materials catalog and project cost accounts.",
            "كتالوج مواد بناء وحسابات تكلفة المشاريع.",
            new[] { "5 categories (Materials, Equipment, Labor, Subcontractors, Tools)",
                    "20 construction SKUs",
                    "Material / Labor / Equipment / Subcontractor cost accounts" },
            new[] { "5 تصنيفات (مواد، معدات، عمالة، مقاولون فرعيون، أدوات)",
                    "20 صنف بناء", "حسابات تكلفة المواد والعمالة والمعدات والمقاولين" }),

        new TemplateInfo(IndustryTemplate.EcommerceStore,"bi-cart",
            "E-commerce Store",  "متجر إلكتروني",
            "Online store with shipping fees + COD reconciliation accounts.",
            "متجر أونلاين مع رسوم شحن وحسابات تسوية الدفع عند الاستلام.",
            new[] { "6 categories (Apparel, Electronics, Home, Beauty, Kids, Gifts)",
                    "30 online SKUs",
                    "Shipping / COD float / Payment gateway fees / Returns accounts" },
            new[] { "6 تصنيفات (ملابس، إلكترونيات، منزلية، تجميل، أطفال، هدايا)",
                    "30 صنف أونلاين", "حسابات الشحن وعهدة الكاش عند الاستلام ورسوم البوابات والمرتجعات" }),
    };

    public async Task<TemplateApplyResult> ApplyAsync(IndustryTemplate template, CancellationToken ct = default)
    {
        var data = BuildSeed(template);

        var existingCategoryNames = await _db.ProductCategories.Select(c => c.NameAr).ToListAsync(ct);
        var existingCategorySet = new HashSet<string>(existingCategoryNames, StringComparer.OrdinalIgnoreCase);

        var addedCategoryNames = new List<string>();
        foreach (var c in data.Categories)
        {
            if (existingCategorySet.Contains(c.NameAr)) continue;
            _db.ProductCategories.Add(new ProductCategory { NameAr = c.NameAr, NameEn = c.NameEn, IsActive = true });
            addedCategoryNames.Add(c.NameAr);
        }
        if (addedCategoryNames.Count > 0) await _db.SaveChangesAsync(ct);

        var categoryByName = await _db.ProductCategories.ToDictionaryAsync(c => c.NameAr, c => c.Id, ct);

        var existingSkus = await _db.Products.Select(p => p.SKU).ToListAsync(ct);
        var existingSkuSet = new HashSet<string>(existingSkus, StringComparer.OrdinalIgnoreCase);

        var productsAdded = 0;
        foreach (var p in data.Products)
        {
            if (existingSkuSet.Contains(p.SKU)) continue;
            int? catId = null;
            if (!string.IsNullOrEmpty(p.CategoryNameAr) && categoryByName.TryGetValue(p.CategoryNameAr, out var id)) catId = id;
            _db.Products.Add(new Product
            {
                SKU = p.SKU, NameAr = p.NameAr, NameEn = p.NameEn,
                Category = p.CategoryNameAr, CategoryId = catId,
                Unit = p.Unit, PurchasePrice = p.PurchasePrice, SellingPrice = p.SellingPrice, VatRate = 15
            });
            productsAdded++;
        }

        var existingCodes = await _db.Accounts.Select(a => a.AccountCode).ToListAsync(ct);
        var existingCodeSet = new HashSet<string>(existingCodes, StringComparer.OrdinalIgnoreCase);

        var accountsAdded = 0;
        foreach (var a in data.Accounts)
        {
            if (existingCodeSet.Contains(a.Code)) continue;
            _db.Accounts.Add(new Account { AccountCode = a.Code, AccountNameAr = a.NameAr, AccountNameEn = a.NameEn, AccountType = a.Type, IsActive = true });
            accountsAdded++;
        }

        if (productsAdded > 0 || accountsAdded > 0) await _db.SaveChangesAsync(ct);
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
                new("المعكرونة",         "Pasta"),
                new("البيتزا",           "Pizza"),
                new("السلطات",           "Salads"),
                new("الأطباق الجانبية",  "Sides"),
                new("الحلويات",          "Desserts"),
                new("المشروبات",         "Beverages"),
            },
            new ProdRow[] {
                new("TPL-RST-001", "حمص",            "Hummus",             "المقبلات",         "طبق", 5m,  18m),
                new("TPL-RST-002", "متبل",           "Mutabbal",           "المقبلات",         "طبق", 6m,  20m),
                new("TPL-RST-003", "موزاريلا ستيكس",  "Mozzarella Sticks",  "المقبلات",         "طبق", 8m,  25m),
                new("TPL-RST-004", "كبسة دجاج",      "Chicken Kabsa",      "الأطباق الرئيسية", "طبق",18m,  45m),
                new("TPL-RST-005", "كبسة لحم",       "Lamb Kabsa",         "الأطباق الرئيسية", "طبق",25m,  65m),
                new("TPL-RST-006", "مندي دجاج",      "Chicken Mandi",      "الأطباق الرئيسية", "طبق",20m,  50m),
                new("TPL-RST-007", "ستيك دجاج مشوي", "Grilled Chicken Steak","الأطباق الرئيسية","طبق",17m, 42m),
                new("TPL-RST-008", "برغر لحم",       "Beef Burger",        "الأطباق الرئيسية", "طبق",18m,  45m),
                new("TPL-RST-009", "برغر دجاج",      "Chicken Burger",     "الأطباق الرئيسية", "طبق",14m,  35m),
                new("TPL-RST-010", "شاورما دجاج",    "Chicken Shawarma",   "الأطباق الرئيسية", "طبق",10m,  25m),
                new("TPL-RST-011", "باستا ألفريدو",  "Pasta Alfredo",      "المعكرونة",        "طبق",14m,  38m),
                new("TPL-RST-012", "باستا بولونيز",  "Pasta Bolognese",    "المعكرونة",        "طبق",15m,  40m),
                new("TPL-RST-013", "لازانيا",        "Lasagna",            "المعكرونة",        "طبق",18m,  48m),
                new("TPL-RST-014", "بيتزا مارغريتا", "Margherita Pizza",   "البيتزا",          "طبق",20m,  50m),
                new("TPL-RST-015", "بيتزا بيبروني",  "Pepperoni Pizza",    "البيتزا",          "طبق",22m,  55m),
                new("TPL-RST-016", "بيتزا خضار",     "Vegetable Pizza",    "البيتزا",          "طبق",18m,  45m),
                new("TPL-RST-017", "سلطة سيزر",      "Caesar Salad",       "السلطات",          "طبق",12m,  30m),
                new("TPL-RST-018", "سلطة يونانية",   "Greek Salad",        "السلطات",          "طبق",13m,  32m),
                new("TPL-RST-019", "سلطة فتوش",      "Fattoush",           "السلطات",          "طبق",10m,  25m),
                new("TPL-RST-020", "بطاطس مقلية",    "French Fries",       "الأطباق الجانبية", "طبق", 5m,  15m),
                new("TPL-RST-021", "أرز أبيض",       "Plain Rice",         "الأطباق الجانبية", "طبق", 4m,  12m),
                new("TPL-RST-022", "خبز ثوم",        "Garlic Bread",       "الأطباق الجانبية", "طبق", 6m,  16m),
                new("TPL-RST-023", "تشيز كيك",       "Cheesecake",         "الحلويات",         "قطعة",10m, 25m),
                new("TPL-RST-024", "تيراميسو",       "Tiramisu",           "الحلويات",         "قطعة",11m, 28m),
                new("TPL-RST-025", "آيس كريم",       "Ice Cream",          "الحلويات",         "كوب", 8m,  20m),
                new("TPL-RST-026", "عصير برتقال",    "Orange Juice",       "المشروبات",        "كوب", 4m,  15m),
                new("TPL-RST-027", "ليمون نعناع",    "Lemon Mint",         "المشروبات",        "كوب", 4m,  18m),
                new("TPL-RST-028", "بيبسي",          "Pepsi",              "المشروبات",        "علبة",2m,   8m),
                new("TPL-RST-029", "ماء معدني",      "Mineral Water",      "المشروبات",        "زجاجة",1m, 5m),
                new("TPL-RST-030", "قهوة عربية",     "Arabic Coffee",      "المشروبات",        "كوب", 3m,  10m),
            },
            new AcctRow[] {
                new("5101", "تكلفة الطعام",     "Food Cost",        AccountType.Expense),
                new("5102", "تكلفة المشروبات",  "Beverage Cost",    AccountType.Expense),
                new("5103", "أجور المطبخ",       "Kitchen Wages",    AccountType.Expense),
                new("5104", "أجور الخدمة",       "Service Wages",    AccountType.Expense),
                new("5105", "تكلفة التوصيل",    "Delivery Cost",    AccountType.Expense),
            }),

        IndustryTemplate.Cafe => new SeedSet(
            new CatRow[] {
                new("إسبريسو",        "Espresso"),
                new("قهوة مفلترة",    "Brewed Coffee"),
                new("مشروبات باردة",  "Cold Drinks"),
                new("قهوة مميزة",     "Specialty"),
                new("شاي وأعشاب",     "Tea & Herbal"),
                new("مخبوزات",        "Bakery"),
            },
            new ProdRow[] {
                new("TPL-CAF-001", "إسبريسو سنجل",    "Single Espresso",   "إسبريسو",        "كوب", 3m,  12m),
                new("TPL-CAF-002", "إسبريسو دبل",     "Double Espresso",   "إسبريسو",        "كوب", 4m,  14m),
                new("TPL-CAF-003", "أمريكانو",        "Americano",         "إسبريسو",        "كوب", 4m,  14m),
                new("TPL-CAF-004", "كابتشينو",        "Cappuccino",        "إسبريسو",        "كوب", 6m,  17m),
                new("TPL-CAF-005", "لاتيه",           "Latte",             "إسبريسو",        "كوب", 7m,  18m),
                new("TPL-CAF-006", "موكا",            "Mocha",             "إسبريسو",        "كوب", 8m,  20m),
                new("TPL-CAF-007", "قهوة فلتر",       "Filter Coffee",     "قهوة مفلترة",    "كوب", 4m,  14m),
                new("TPL-CAF-008", "في60",            "V60",               "قهوة مفلترة",    "كوب", 7m,  22m),
                new("TPL-CAF-009", "كولد برو",        "Cold Brew",         "مشروبات باردة",  "كوب", 7m,  20m),
                new("TPL-CAF-010", "آيس لاتيه",       "Iced Latte",        "مشروبات باردة",  "كوب", 7m,  19m),
                new("TPL-CAF-011", "فرابوتشينو",      "Frappuccino",       "مشروبات باردة",  "كوب", 9m,  24m),
                new("TPL-CAF-012", "آيس كراميل",      "Iced Caramel",      "مشروبات باردة",  "كوب", 9m,  24m),
                new("TPL-CAF-013", "ماتشا لاتيه",     "Matcha Latte",      "قهوة مميزة",     "كوب",10m,  26m),
                new("TPL-CAF-014", "حليب الذهب",      "Golden Milk",       "قهوة مميزة",     "كوب",10m,  25m),
                new("TPL-CAF-015", "سبانش لاتيه",     "Spanish Latte",     "قهوة مميزة",     "كوب", 9m,  22m),
                new("TPL-CAF-016", "شاي أحمر",        "Black Tea",         "شاي وأعشاب",     "كوب", 2m,   8m),
                new("TPL-CAF-017", "شاي أخضر",        "Green Tea",         "شاي وأعشاب",     "كوب", 3m,  10m),
                new("TPL-CAF-018", "بابونج",          "Chamomile",         "شاي وأعشاب",     "كوب", 3m,  10m),
                new("TPL-CAF-019", "كرواسون",         "Croissant",         "مخبوزات",        "قطعة", 4m, 12m),
                new("TPL-CAF-020", "ساندويتش جبن",    "Cheese Sandwich",   "مخبوزات",        "قطعة", 8m, 22m),
                new("TPL-CAF-021", "كوكيز شوكولاتة",  "Chocolate Cookie",  "مخبوزات",        "قطعة", 3m, 10m),
                new("TPL-CAF-022", "مافن",            "Muffin",            "مخبوزات",        "قطعة", 4m, 12m),
                new("TPL-CAF-023", "بان كيك",         "Pancakes",          "مخبوزات",        "طبق",  9m, 25m),
                new("TPL-CAF-024", "وافل",            "Waffle",            "مخبوزات",        "طبق",10m,  28m),
                new("TPL-CAF-025", "فطور كافيه",      "Café Breakfast",    "مخبوزات",        "طبق",18m,  45m),
            },
            new AcctRow[] {
                new("5111", "حبوب القهوة",       "Coffee Beans",       AccountType.Expense),
                new("5112", "الحليب والكريما",   "Milk & Cream",       AccountType.Expense),
                new("5113", "المعجنات والمخبوزات","Pastries & Bakery",  AccountType.Expense),
                new("5114", "أجور الباريستا",    "Barista Wages",      AccountType.Expense),
            }),

        IndustryTemplate.Bakery => new SeedSet(
            new CatRow[] {
                new("الخبز",         "Bread"),
                new("المعجنات",      "Pastries"),
                new("الكيك",         "Cakes"),
                new("الكوكيز",       "Cookies"),
                new("طلبات خاصة",    "Custom Orders"),
            },
            new ProdRow[] {
                new("TPL-BAK-001", "خبز عربي",        "Arabic Bread",      "الخبز",      "كيس",  2m,   5m),
                new("TPL-BAK-002", "خبز توست",         "Toast Bread",       "الخبز",      "كيس",  4m,  10m),
                new("TPL-BAK-003", "خبز فرنسي",        "Baguette",          "الخبز",      "قطعة", 4m,  12m),
                new("TPL-BAK-004", "خبز برغر",         "Burger Buns",       "الخبز",      "حزمة", 5m,  14m),
                new("TPL-BAK-005", "كرواسون زبدة",     "Butter Croissant",  "المعجنات",   "قطعة", 4m,  10m),
                new("TPL-BAK-006", "فطيرة زعتر",       "Zaatar Manakish",   "المعجنات",   "قطعة", 3m,   8m),
                new("TPL-BAK-007", "فطيرة جبن",        "Cheese Manakish",   "المعجنات",   "قطعة", 4m,  10m),
                new("TPL-BAK-008", "بقلاوة",           "Baklava",           "المعجنات",   "كيلو",30m,  85m),
                new("TPL-BAK-009", "كيك شوكولاتة",     "Chocolate Cake",    "الكيك",      "كيلو",35m,  90m),
                new("TPL-BAK-010", "كيك فانيلا",        "Vanilla Cake",      "الكيك",      "كيلو",30m,  80m),
                new("TPL-BAK-011", "تشيز كيك",         "Cheesecake",        "الكيك",      "كيلو",40m, 110m),
                new("TPL-BAK-012", "ريد فيلفيت",       "Red Velvet",        "الكيك",      "كيلو",38m, 100m),
                new("TPL-BAK-013", "كب كيك",           "Cupcake",           "الكيك",      "قطعة", 4m,  12m),
                new("TPL-BAK-014", "كوكيز شوكولاتة",   "Chocolate Cookie",  "الكوكيز",    "قطعة", 2m,   7m),
                new("TPL-BAK-015", "كوكيز شوفان",       "Oatmeal Cookie",    "الكوكيز",    "قطعة", 2m,   8m),
                new("TPL-BAK-016", "ماكرون",           "Macarons",          "الكوكيز",    "قطعة", 4m,  14m),
                new("TPL-BAK-017", "كيك زفاف",         "Wedding Cake",      "طلبات خاصة", "قطعة",250m, 750m),
                new("TPL-BAK-018", "كيك عيد ميلاد",    "Birthday Cake",     "طلبات خاصة", "قطعة",80m,  220m),
                new("TPL-BAK-019", "صينية معجنات مشكلة","Mixed Pastry Tray", "طلبات خاصة", "صينية",60m,160m),
                new("TPL-BAK-020", "كاتر بريك",        "Cake Pop",          "طلبات خاصة", "قطعة", 5m,  15m),
            },
            new AcctRow[] {
                new("5121", "الدقيق والحبوب",        "Flour & Grains",    AccountType.Expense),
                new("5122", "السكر",                 "Sugar",             AccountType.Expense),
                new("5123", "منتجات الألبان",        "Dairy",             AccountType.Expense),
                new("5124", "التغليف",               "Packaging",         AccountType.Expense),
            }),

        IndustryTemplate.Trading => new SeedSet(
            new CatRow[] {
                new("مواد غذائية",      "Groceries"),
                new("إلكترونيات",       "Electronics"),
                new("ملابس",            "Clothing"),
                new("أدوات منزلية",     "Home Goods"),
                new("مستلزمات مكتبية",  "Office Supplies"),
                new("ألعاب وهدايا",     "Toys & Gifts"),
                new("صحة",              "Health"),
                new("تجميل",            "Beauty"),
            },
            new ProdRow[] {
                new("TPL-TRD-001", "أرز بسمتي 5 كجم",  "Basmati Rice 5kg",  "مواد غذائية",      "كيس",  35m,  55m),
                new("TPL-TRD-002", "زيت زيتون 1 لتر",  "Olive Oil 1L",      "مواد غذائية",      "زجاجة",22m,  38m),
                new("TPL-TRD-003", "تمر مجدول 1 كجم",  "Medjool Dates 1kg", "مواد غذائية",      "كيس",  60m, 110m),
                new("TPL-TRD-004", "عسل سدر 500 جرام", "Sidr Honey 500g",   "مواد غذائية",      "علبة",110m, 195m),
                new("TPL-TRD-005", "سماعة بلوتوث",     "Bluetooth Headset", "إلكترونيات",       "قطعة", 60m, 145m),
                new("TPL-TRD-006", "شاحن سريع 30 واط", "Fast Charger 30W",  "إلكترونيات",       "قطعة", 35m,  79m),
                new("TPL-TRD-007", "كيبل USB-C",       "USB-C Cable",       "إلكترونيات",       "قطعة", 12m,  29m),
                new("TPL-TRD-008", "بطاقة ذاكرة 64GB", "Memory Card 64GB",  "إلكترونيات",       "قطعة", 28m,  59m),
                new("TPL-TRD-009", "قميص قطني",        "Cotton Shirt",      "ملابس",            "قطعة", 45m, 120m),
                new("TPL-TRD-010", "بنطلون جينز",      "Denim Jeans",       "ملابس",            "قطعة", 80m, 195m),
                new("TPL-TRD-011", "ثوب رجالي",        "Men's Thobe",       "ملابس",            "قطعة",110m, 245m),
                new("TPL-TRD-012", "حذاء رياضي",       "Sneakers",          "ملابس",            "قطعة", 95m, 220m),
                new("TPL-TRD-013", "مقلاة 28 سم",      "Frying Pan 28cm",   "أدوات منزلية",     "قطعة", 55m, 119m),
                new("TPL-TRD-014", "طقم سكاكين",       "Knife Set",         "أدوات منزلية",     "طقم",  90m, 175m),
                new("TPL-TRD-015", "مكنسة كهربائية",   "Vacuum Cleaner",    "أدوات منزلية",     "قطعة",220m, 420m),
                new("TPL-TRD-016", "ماكينة قهوة",      "Coffee Maker",      "أدوات منزلية",     "قطعة",180m, 349m),
                new("TPL-TRD-017", "رزمة ورق A4",      "A4 Paper Ream",     "مستلزمات مكتبية",  "رزمة", 12m,  22m),
                new("TPL-TRD-018", "قلم حبر جاف",      "Ballpoint Pen",     "مستلزمات مكتبية",  "قطعة",  1m,    3m),
                new("TPL-TRD-019", "دباسة مكتبية",     "Office Stapler",    "مستلزمات مكتبية",  "قطعة", 18m,  39m),
                new("TPL-TRD-020", "لوح ميكا A3",      "A3 Whiteboard",     "مستلزمات مكتبية",  "قطعة",110m, 220m),
                new("TPL-TRD-021", "لعبة تركيب",       "Lego-style Set",    "ألعاب وهدايا",     "علبة", 75m, 169m),
                new("TPL-TRD-022", "دمية قماش",        "Plush Toy",         "ألعاب وهدايا",     "قطعة", 25m,  60m),
                new("TPL-TRD-023", "لعبة ألغاز",       "Puzzle 1000pc",     "ألعاب وهدايا",     "علبة", 35m,  79m),
                new("TPL-TRD-024", "مقياس ضغط",        "Blood Pressure Meter","صحة",            "قطعة",110m, 220m),
                new("TPL-TRD-025", "ميزان رقمي",       "Digital Scale",     "صحة",              "قطعة", 65m, 129m),
                new("TPL-TRD-026", "مكمل فيتامين سي",  "Vitamin C 60ct",    "صحة",              "علبة", 35m,  79m),
                new("TPL-TRD-027", "كريم مرطب",        "Moisturizer",       "تجميل",            "قطعة", 40m,  95m),
                new("TPL-TRD-028", "عطر رجالي",        "Men's Cologne",     "تجميل",            "قطعة",180m, 369m),
                new("TPL-TRD-029", "أحمر شفاه",        "Lipstick",          "تجميل",            "قطعة", 25m,  65m),
                new("TPL-TRD-030", "شامبو",             "Shampoo 400ml",     "تجميل",            "زجاجة",18m,  39m),
                new("TPL-TRD-031", "ساعة جدارية",       "Wall Clock",        "أدوات منزلية",     "قطعة", 45m,  99m),
                new("TPL-TRD-032", "بطانية صوف",        "Wool Blanket",      "أدوات منزلية",     "قطعة", 80m, 169m),
            },
            new AcctRow[] {
                new("5201", "تكلفة البضاعة المباعة", "Cost of Goods Sold", AccountType.Expense),
                new("5202", "مرتجعات المبيعات",      "Sales Returns",      AccountType.Revenue),
                new("5203", "خصم الكمية",            "Quantity Discount",  AccountType.Expense),
                new("5204", "شحن وارد",              "Freight In",         AccountType.Expense),
                new("5205", "شحن صادر",              "Freight Out",        AccountType.Expense),
            }),

        IndustryTemplate.Pharmacy => new SeedSet(
            new CatRow[] {
                new("مسكنات",            "Pain Relief"),
                new("نزلات البرد",       "Cold & Flu"),
                new("فيتامينات",         "Vitamins"),
                new("عناية بالبشرة",     "Skincare"),
                new("منتجات الأطفال",    "Baby"),
                new("الإسعافات الأولية", "First Aid"),
            },
            new ProdRow[] {
                new("TPL-PHA-001", "بنادول 500 مجم",       "Panadol 500mg",      "مسكنات",         "علبة", 6m,  12m),
                new("TPL-PHA-002", "بروفين 400 مجم",       "Profen 400mg",       "مسكنات",         "علبة", 8m,  16m),
                new("TPL-PHA-003", "أسبرين 100 مجم",       "Aspirin 100mg",      "مسكنات",         "علبة", 9m,  18m),
                new("TPL-PHA-004", "فولتارين جل",          "Voltaren Gel",       "مسكنات",         "أنبوب",18m, 35m),
                new("TPL-PHA-005", "مكسيكولد",             "MaxiCold",           "نزلات البرد",    "علبة",12m,  24m),
                new("TPL-PHA-006", "بخاخ أنف",             "Nasal Spray",        "نزلات البرد",    "زجاجة",15m,30m),
                new("TPL-PHA-007", "شراب سعال",            "Cough Syrup",        "نزلات البرد",    "زجاجة",18m,36m),
                new("TPL-PHA-008", "حبوب فيتامين C",       "Vitamin C 1000",     "فيتامينات",      "علبة",30m,  59m),
                new("TPL-PHA-009", "حبوب أوميغا 3",        "Omega 3",            "فيتامينات",      "علبة",55m, 110m),
                new("TPL-PHA-010", "ملتي فيتامين",          "Multivitamin",       "فيتامينات",      "علبة",45m,  89m),
                new("TPL-PHA-011", "حبوب حديد",             "Iron Tablets",       "فيتامينات",      "علبة",18m,  39m),
                new("TPL-PHA-012", "حبوب كالسيوم D3",      "Calcium D3",         "فيتامينات",      "علبة",22m,  45m),
                new("TPL-PHA-013", "كريم مرطب نيفيا",       "Nivea Moisturizer",  "عناية بالبشرة",  "علبة",18m,  35m),
                new("TPL-PHA-014", "واقي شمس SPF50",        "Sunscreen SPF50",    "عناية بالبشرة",  "أنبوب",45m, 89m),
                new("TPL-PHA-015", "غسول وجه",              "Face Wash",          "عناية بالبشرة",  "زجاجة",22m, 45m),
                new("TPL-PHA-016", "كريم بانثينول",         "Panthenol Cream",    "عناية بالبشرة",  "أنبوب",16m, 32m),
                new("TPL-PHA-017", "حليب أطفال 1",          "Baby Formula 1",     "منتجات الأطفال", "علبة",70m, 119m),
                new("TPL-PHA-018", "حفاضات",                 "Diapers",            "منتجات الأطفال", "كيس",55m,  95m),
                new("TPL-PHA-019", "رضاعة سيليكون",          "Silicone Bottle",    "منتجات الأطفال", "قطعة",30m, 65m),
                new("TPL-PHA-020", "مناديل أطفال",           "Baby Wipes",         "منتجات الأطفال", "علبة",10m, 22m),
                new("TPL-PHA-021", "ضمادات",                 "Bandages",           "الإسعافات الأولية","علبة",6m,15m),
                new("TPL-PHA-022", "بخاخ مطهر",              "Antiseptic Spray",   "الإسعافات الأولية","زجاجة",12m,28m),
                new("TPL-PHA-023", "ميزان حرارة",            "Thermometer",        "الإسعافات الأولية","قطعة",25m,55m),
                new("TPL-PHA-024", "كمادات باردة",           "Cold Compress",      "الإسعافات الأولية","قطعة",15m,32m),
                new("TPL-PHA-025", "قفازات طبية",            "Medical Gloves",     "الإسعافات الأولية","علبة",18m,35m),
            },
            new AcctRow[] {
                new("5211", "تكلفة الأدوية",     "Pharmaceutical Cost", AccountType.Expense),
                new("5212", "تكلفة التبريد",     "Cold-chain Cost",     AccountType.Expense),
                new("5213", "مرتجعات للموردين",  "Returns to Supplier", AccountType.Expense),
            }),

        IndustryTemplate.Clinic => new SeedSet(
            new CatRow[] {
                new("استشارة عامة",     "General Consultation"),
                new("استشارة أخصائي",   "Specialist Consultation"),
                new("مختبر",            "Laboratory"),
                new("إجراءات",          "Procedures"),
                new("متابعة",           "Follow-up"),
            },
            new ProdRow[] {
                new("TPL-CLN-001", "استشارة عامة 30 د",  "General Consult 30min",  "استشارة عامة",   "زيارة", 0m, 200m),
                new("TPL-CLN-002", "استشارة عامة عاجلة", "Urgent Consult",         "استشارة عامة",   "زيارة", 0m, 350m),
                new("TPL-CLN-003", "استشارة باطنية",     "Internal Medicine",      "استشارة أخصائي", "زيارة", 0m, 400m),
                new("TPL-CLN-004", "استشارة جلدية",      "Dermatology",            "استشارة أخصائي", "زيارة", 0m, 450m),
                new("TPL-CLN-005", "استشارة أطفال",      "Pediatrics",             "استشارة أخصائي", "زيارة", 0m, 350m),
                new("TPL-CLN-006", "استشارة نسائية",     "Gynaecology",            "استشارة أخصائي", "زيارة", 0m, 500m),
                new("TPL-CLN-007", "تحليل دم شامل",      "Complete Blood Count",   "مختبر",          "تحليل", 0m, 150m),
                new("TPL-CLN-008", "تحليل سكر",          "Blood Sugar",            "مختبر",          "تحليل", 0m,  60m),
                new("TPL-CLN-009", "تحليل بول",          "Urine Analysis",         "مختبر",          "تحليل", 0m,  70m),
                new("TPL-CLN-010", "تحليل غدة درقية",    "Thyroid Panel",          "مختبر",          "تحليل", 0m, 220m),
                new("TPL-CLN-011", "حقنة وريدية",         "IV Injection",           "إجراءات",        "إجراء", 0m, 120m),
                new("TPL-CLN-012", "تخييط جرح",           "Wound Suture",           "إجراءات",        "إجراء", 0m, 250m),
                new("TPL-CLN-013", "إزالة شامة",          "Mole Removal",           "إجراءات",        "إجراء", 0m, 600m),
                new("TPL-CLN-014", "زيارة متابعة",        "Follow-up Visit",        "متابعة",         "زيارة", 0m, 100m),
                new("TPL-CLN-015", "متابعة هاتفية",       "Phone Follow-up",        "متابعة",         "مكالمة",0m, 75m),
            },
            new AcctRow[] {
                new("4301", "إيرادات الاستشارات",   "Consultation Revenue",  AccountType.Revenue),
                new("4302", "إيرادات المختبر",      "Lab Revenue",           AccountType.Revenue),
                new("5301", "تكلفة المختبر",        "Lab Costs",             AccountType.Expense),
                new("1301", "ذمم شركات التأمين",    "Insurance Receivables", AccountType.Asset),
            }),

        IndustryTemplate.Salon => new SeedSet(
            new CatRow[] {
                new("الشعر",     "Hair"),
                new("الأظافر",   "Nails"),
                new("الوجه",     "Facials"),
                new("المساج",    "Massage"),
                new("المكياج",   "Makeup"),
                new("منتجات",    "Retail"),
            },
            new ProdRow[] {
                new("TPL-SAL-001", "قص شعر",           "Haircut",            "الشعر",   "خدمة",  0m,  80m),
                new("TPL-SAL-002", "صبغة شعر",         "Hair Coloring",      "الشعر",   "خدمة", 30m, 250m),
                new("TPL-SAL-003", "كيراتين",          "Keratin Treatment",  "الشعر",   "خدمة", 80m, 600m),
                new("TPL-SAL-004", "تسريحة",           "Styling",            "الشعر",   "خدمة",  0m, 150m),
                new("TPL-SAL-005", "مناكير",           "Manicure",           "الأظافر", "خدمة",  0m, 100m),
                new("TPL-SAL-006", "بديكير",           "Pedicure",           "الأظافر", "خدمة",  0m, 120m),
                new("TPL-SAL-007", "أظافر جل",         "Gel Nails",          "الأظافر", "خدمة", 15m, 180m),
                new("TPL-SAL-008", "تنظيف بشرة",       "Facial Cleansing",   "الوجه",   "خدمة", 25m, 250m),
                new("TPL-SAL-009", "ماسك بشرة",        "Face Mask",          "الوجه",   "خدمة", 15m, 150m),
                new("TPL-SAL-010", "تشقير",            "Bleaching",          "الوجه",   "خدمة", 10m,  80m),
                new("TPL-SAL-011", "مساج 30 د",        "Massage 30min",      "المساج",  "خدمة",  0m, 200m),
                new("TPL-SAL-012", "مساج 60 د",        "Massage 60min",      "المساج",  "خدمة",  0m, 350m),
                new("TPL-SAL-013", "حمام مغربي",       "Moroccan Bath",      "المساج",  "خدمة", 30m, 400m),
                new("TPL-SAL-014", "مكياج مناسبة",     "Event Makeup",       "المكياج", "خدمة", 20m, 350m),
                new("TPL-SAL-015", "مكياج عرايس",      "Bridal Makeup",      "المكياج", "خدمة", 50m,1500m),
                new("TPL-SAL-016", "شامبو مهني",        "Pro Shampoo",        "منتجات",  "زجاجة",30m, 75m),
                new("TPL-SAL-017", "زيت شعر",          "Hair Oil",           "منتجات",  "زجاجة",25m,  65m),
                new("TPL-SAL-018", "كريم وجه",         "Face Cream",         "منتجات",  "علبة", 40m, 110m),
                new("TPL-SAL-019", "أحمر شفاه فاخر",    "Premium Lipstick",   "منتجات",  "قطعة", 35m,  95m),
                new("TPL-SAL-020", "عطر",              "Perfume",            "منتجات",  "زجاجة",80m, 220m),
            },
            new AcctRow[] {
                new("5311", "عمولات المصففين",    "Stylist Commissions", AccountType.Expense),
                new("5312", "تكلفة المنتجات",     "Product COGS",        AccountType.Expense),
                new("4311", "إيرادات الخدمات",    "Service Revenue",     AccountType.Revenue),
            }),

        IndustryTemplate.Gym => new SeedSet(
            new CatRow[] {
                new("اشتراكات",      "Memberships"),
                new("دورات",         "Classes"),
                new("تدريب شخصي",    "Personal Training"),
                new("مكملات",        "Supplements"),
                new("ملابس رياضية",  "Apparel"),
            },
            new ProdRow[] {
                new("TPL-GYM-001", "اشتراك شهري",     "Monthly Membership",   "اشتراكات",      "شهر",   0m, 350m),
                new("TPL-GYM-002", "اشتراك ربع سنوي",  "Quarterly Membership", "اشتراكات",      "3 أشهر",0m, 950m),
                new("TPL-GYM-003", "اشتراك سنوي",     "Annual Membership",    "اشتراكات",      "سنة",   0m,3200m),
                new("TPL-GYM-004", "اشتراك عائلي",    "Family Membership",    "اشتراكات",      "شهر",   0m, 850m),
                new("TPL-GYM-005", "حصة كروسفت",      "CrossFit Class",       "دورات",         "حصة",   0m,  60m),
                new("TPL-GYM-006", "حصة يوغا",        "Yoga Class",           "دورات",         "حصة",   0m,  50m),
                new("TPL-GYM-007", "حصة بيلاتس",      "Pilates Class",        "دورات",         "حصة",   0m,  60m),
                new("TPL-GYM-008", "حصة بوكسينج",     "Boxing Class",         "دورات",         "حصة",   0m,  70m),
                new("TPL-GYM-009", "حصة سبينينج",     "Spinning Class",       "دورات",         "حصة",   0m,  55m),
                new("TPL-GYM-010", "تدريب شخصي 1",    "PT 1 Session",         "تدريب شخصي",    "حصة",   0m, 200m),
                new("TPL-GYM-011", "تدريب شخصي 5",    "PT 5 Sessions",        "تدريب شخصي",    "5 حصص", 0m, 900m),
                new("TPL-GYM-012", "تدريب شخصي 10",   "PT 10 Sessions",       "تدريب شخصي",    "10 حصص",0m,1700m),
                new("TPL-GYM-013", "بروتين واي",      "Whey Protein",         "مكملات",        "علبة",110m, 250m),
                new("TPL-GYM-014", "كرياتين",         "Creatine",             "مكملات",        "علبة", 60m, 130m),
                new("TPL-GYM-015", "بي سي إيه ايه",   "BCAA",                 "مكملات",        "علبة", 80m, 175m),
                new("TPL-GYM-016", "بروتين بار",      "Protein Bar",          "مكملات",        "قطعة",  6m,  15m),
                new("TPL-GYM-017", "تيشيرت رياضي",    "Athletic T-shirt",     "ملابس رياضية",  "قطعة", 40m,  99m),
                new("TPL-GYM-018", "شورت رياضي",       "Athletic Shorts",      "ملابس رياضية",  "قطعة", 35m,  85m),
            },
            new AcctRow[] {
                new("5321", "أجور المدربين",       "Trainer Wages",          AccountType.Expense),
                new("5322", "صيانة المعدات",      "Equipment Maintenance",  AccountType.Expense),
                new("5323", "تكلفة المكملات",      "Supplement COGS",        AccountType.Expense),
                new("4321", "إيرادات الاشتراكات", "Membership Revenue",     AccountType.Revenue),
            }),

        IndustryTemplate.AutoRepair => new SeedSet(
            new CatRow[] {
                new("محرك",          "Engine"),
                new("فرامل",         "Brakes"),
                new("تعليق",         "Suspension"),
                new("كهرباء",        "Electrical"),
                new("الصدامات",      "Body"),
                new("الزيوت",        "Lubricants"),
            },
            new ProdRow[] {
                new("TPL-AUT-001", "تغيير زيت محرك",     "Oil Change Service",   "محرك",   "خدمة", 80m, 180m),
                new("TPL-AUT-002", "فلتر زيت",           "Oil Filter",           "محرك",   "قطعة", 15m,  40m),
                new("TPL-AUT-003", "فلتر هواء",          "Air Filter",           "محرك",   "قطعة", 20m,  50m),
                new("TPL-AUT-004", "بواجي",              "Spark Plugs (4)",      "محرك",   "طقم",  60m, 140m),
                new("TPL-AUT-005", "حساس أوكسجين",       "O2 Sensor",            "محرك",   "قطعة",120m, 280m),
                new("TPL-AUT-006", "تيل فرامل أمامي",    "Front Brake Pads",     "فرامل",  "طقم", 110m, 250m),
                new("TPL-AUT-007", "تيل فرامل خلفي",     "Rear Brake Pads",      "فرامل",  "طقم",  90m, 210m),
                new("TPL-AUT-008", "ديسك فرامل",         "Brake Disc",           "فرامل",  "قطعة",180m, 380m),
                new("TPL-AUT-009", "زيت فرامل",          "Brake Fluid",          "فرامل",  "علبة", 25m,  55m),
                new("TPL-AUT-010", "مساعدات أمامية",     "Front Shocks (pair)",  "تعليق",  "طقم", 350m, 750m),
                new("TPL-AUT-011", "مساعدات خلفية",      "Rear Shocks (pair)",   "تعليق",  "طقم", 320m, 700m),
                new("TPL-AUT-012", "كوسيات تعليق",       "Suspension Bushings",  "تعليق",  "طقم", 100m, 220m),
                new("TPL-AUT-013", "بطارية 70 أمبير",    "Battery 70Ah",         "كهرباء", "قطعة",230m, 420m),
                new("TPL-AUT-014", "دينامو",             "Alternator",           "كهرباء", "قطعة",380m, 750m),
                new("TPL-AUT-015", "موتور سلف",          "Starter Motor",        "كهرباء", "قطعة",350m, 700m),
                new("TPL-AUT-016", "كشاف أمامي",         "Headlight Assembly",   "الصدامات","قطعة",450m, 950m),
                new("TPL-AUT-017", "زجاج أمامي",         "Windshield",           "الصدامات","قطعة",550m,1200m),
                new("TPL-AUT-018", "صدام أمامي",         "Front Bumper",         "الصدامات","قطعة",450m,1100m),
                new("TPL-AUT-019", "زيت محرك 5W30 4ل",   "Engine Oil 5W30 4L",   "الزيوت", "علبة",100m, 220m),
                new("TPL-AUT-020", "زيت ATF",            "ATF Fluid",            "الزيوت", "لتر",  35m,  75m),
                new("TPL-AUT-021", "جل قابلية",          "Coolant",              "الزيوت", "لتر",  18m,  40m),
                new("TPL-AUT-022", "ساعة عمل ميكانيكي",  "Mechanic Labour Hour", "محرك",   "ساعة",  0m, 150m),
                new("TPL-AUT-023", "ساعة عمل كهربائي",   "Electrician Hour",     "كهرباء", "ساعة",  0m, 180m),
                new("TPL-AUT-024", "ساعة عمل بودي",      "Body Shop Hour",       "الصدامات","ساعة", 0m, 200m),
                new("TPL-AUT-025", "فحص شامل",            "Full Inspection",      "محرك",   "خدمة", 30m, 200m),
            },
            new AcctRow[] {
                new("5331", "تكلفة قطع الغيار",   "Spare Parts COGS",   AccountType.Expense),
                new("4331", "إيرادات العمالة",    "Labor Revenue",      AccountType.Revenue),
                new("2331", "ضمانات لاحقة",       "Warranty Provision", AccountType.Liability),
            }),

        IndustryTemplate.Printing => new SeedSet(
            new CatRow[] {
                new("بطاقات أعمال",   "Business Cards"),
                new("لافتات",         "Banners"),
                new("كتب وكتيبات",    "Books & Booklets"),
                new("مكتبية",         "Office Supplies"),
                new("طلبات خاصة",     "Custom Orders"),
            },
            new ProdRow[] {
                new("TPL-PRT-001", "بطاقات 100",         "Business Cards 100",   "بطاقات أعمال",  "حزمة", 40m,  90m),
                new("TPL-PRT-002", "بطاقات 500",         "Business Cards 500",   "بطاقات أعمال",  "حزمة",100m, 250m),
                new("TPL-PRT-003", "بطاقات معدنية",       "Metal Cards",          "بطاقات أعمال",  "حزمة",250m, 580m),
                new("TPL-PRT-004", "لافتة فينيل 1×3",    "Vinyl Banner 1×3m",    "لافتات",        "قطعة",100m, 280m),
                new("TPL-PRT-005", "لافتة قماش 2×3",     "Fabric Banner 2×3m",   "لافتات",        "قطعة",180m, 450m),
                new("TPL-PRT-006", "رول أب",             "Roll-up Stand",        "لافتات",        "قطعة",200m, 480m),
                new("TPL-PRT-007", "لوحة فلكس مضيئة",     "Lit Flex Sign",        "لافتات",        "م²",  280m, 650m),
                new("TPL-PRT-008", "كتاب 50 صفحة A5",    "50pg Book A5",         "كتب وكتيبات",   "نسخة", 12m,  35m),
                new("TPL-PRT-009", "كتاب 100 صفحة A5",   "100pg Book A5",        "كتب وكتيبات",   "نسخة", 22m,  60m),
                new("TPL-PRT-010", "كتيب 8 صفحات",       "Booklet 8pg",          "كتب وكتيبات",   "نسخة",  6m,  18m),
                new("TPL-PRT-011", "بروشور مطوي",         "Folded Brochure",      "كتب وكتيبات",   "نسخة",  4m,  10m),
                new("TPL-PRT-012", "ورق A4 رزمة",        "A4 Paper Ream",        "مكتبية",        "رزمة", 12m,  22m),
                new("TPL-PRT-013", "تونر ليزر",           "Laser Toner",          "مكتبية",        "علبة",180m, 320m),
                new("TPL-PRT-014", "حبر أنكجت",           "Inkjet Cartridge",     "مكتبية",        "علبة", 90m, 180m),
                new("TPL-PRT-015", "فايل تقديم",          "Presentation Folder",  "مكتبية",        "قطعة", 12m,  28m),
                new("TPL-PRT-016", "إستيكر دائري 5 سم",   "Round Sticker 5cm",    "طلبات خاصة",    "حزمة 100",30m,80m),
                new("TPL-PRT-017", "أكواب مطبوعة",        "Printed Mugs",         "طلبات خاصة",    "قطعة", 18m,  45m),
                new("TPL-PRT-018", "تيشرتات مطبوعة",       "Printed T-shirts",     "طلبات خاصة",    "قطعة", 35m,  85m),
                new("TPL-PRT-019", "دعوات أفراح",          "Wedding Invitations",  "طلبات خاصة",    "حزمة 50",80m,220m),
                new("TPL-PRT-020", "تغليف هدايا",          "Gift Wrapping",        "طلبات خاصة",    "خدمة",  5m,  15m),
            },
            new AcctRow[] {
                new("5341", "تكلفة الورق",        "Paper Cost",          AccountType.Expense),
                new("5342", "تكلفة الحبر/تونر",   "Ink/Toner Cost",      AccountType.Expense),
                new("5343", "طباعة خارجية",      "Outsourced Printing", AccountType.Expense),
            }),

        IndustryTemplate.EducationCenter => new SeedSet(
            new CatRow[] {
                new("لغات",       "Languages"),
                new("تقنية",      "IT"),
                new("أعمال",      "Business"),
                new("أطفال",      "Kids"),
                new("شهادات",     "Certifications"),
            },
            new ProdRow[] {
                new("TPL-EDU-001", "إنجليزي ابتدائي",    "English Beginner",    "لغات",   "دورة", 0m, 1500m),
                new("TPL-EDU-002", "إنجليزي محادثة",     "English Conversation","لغات",   "دورة", 0m, 1800m),
                new("TPL-EDU-003", "إنجليزي IELTS",      "IELTS Prep",          "لغات",   "دورة", 0m, 2500m),
                new("TPL-EDU-004", "عربية لغير الناطقين","Arabic for Foreigners","لغات",  "دورة", 0m, 2000m),
                new("TPL-EDU-005", "بايثون أساسيات",     "Python Basics",       "تقنية",  "دورة", 0m, 1800m),
                new("TPL-EDU-006", "تطوير ويب",          "Web Development",     "تقنية",  "دورة", 0m, 3500m),
                new("TPL-EDU-007", "علم البيانات",        "Data Science",        "تقنية",  "دورة", 0m, 4500m),
                new("TPL-EDU-008", "أمن سيبراني أساسي",  "Cybersecurity Basic", "تقنية",  "دورة", 0m, 2800m),
                new("TPL-EDU-009", "محاسبة عملية",       "Practical Accounting","أعمال",  "دورة", 0m, 1600m),
                new("TPL-EDU-010", "إدارة مشاريع",        "Project Management",  "أعمال",  "دورة", 0m, 2200m),
                new("TPL-EDU-011", "تسويق رقمي",         "Digital Marketing",   "أعمال",  "دورة", 0m, 1900m),
                new("TPL-EDU-012", "حساب أطفال",         "Kids Math Camp",      "أطفال",  "دورة", 0m,  900m),
                new("TPL-EDU-013", "روبوتيك أطفال",      "Kids Robotics",       "أطفال",  "دورة", 0m, 1300m),
                new("TPL-EDU-014", "PMP",                "PMP Prep",            "شهادات", "دورة", 0m, 4500m),
                new("TPL-EDU-015", "AWS Cloud",          "AWS Cloud Practitioner","شهادات","دورة",0m,3200m),
            },
            new AcctRow[] {
                new("5351", "أتعاب المدربين",       "Instructor Fees",   AccountType.Expense),
                new("5352", "تكلفة المواد",         "Course Materials",  AccountType.Expense),
                new("4351", "إيرادات الدورات",      "Course Revenue",    AccountType.Revenue),
                new("4352", "إيرادات الشهادات",     "Certification Revenue",AccountType.Revenue),
            }),

        IndustryTemplate.Hotel => new SeedSet(
            new CatRow[] {
                new("غرف",         "Rooms"),
                new("مطعم",        "Restaurant"),
                new("فعاليات",     "Events"),
                new("سبا",         "Spa"),
                new("مغسلة",       "Laundry"),
            },
            new ProdRow[] {
                new("TPL-HTL-001", "غرفة عادية",        "Standard Room",         "غرف",     "ليلة",  0m, 350m),
                new("TPL-HTL-002", "غرفة دلوكس",        "Deluxe Room",           "غرف",     "ليلة",  0m, 550m),
                new("TPL-HTL-003", "غرفة عائلية",       "Family Room",           "غرف",     "ليلة",  0m, 800m),
                new("TPL-HTL-004", "جناح",              "Suite",                 "غرف",     "ليلة",  0m,1400m),
                new("TPL-HTL-005", "إقامة طويلة",       "Long-stay Rate",        "غرف",     "أسبوع", 0m,2200m),
                new("TPL-HTL-006", "إفطار بوفيه",       "Buffet Breakfast",      "مطعم",    "شخص",   0m,  85m),
                new("TPL-HTL-007", "غداء بوفيه",        "Buffet Lunch",          "مطعم",    "شخص",   0m, 120m),
                new("TPL-HTL-008", "عشاء بوفيه",        "Buffet Dinner",         "مطعم",    "شخص",   0m, 145m),
                new("TPL-HTL-009", "خدمة غرف",          "Room Service",          "مطعم",    "خدمة",  0m,  35m),
                new("TPL-HTL-010", "قاعة صغيرة 4 س",    "Small Hall 4hr",        "فعاليات", "حدث",   0m,2500m),
                new("TPL-HTL-011", "قاعة كبيرة 4 س",    "Grand Hall 4hr",        "فعاليات", "حدث",   0m,8500m),
                new("TPL-HTL-012", "بوفيه مناسبة 50 ش",  "Event Buffet 50pax",    "فعاليات", "حدث",   0m,7500m),
                new("TPL-HTL-013", "مساج 60 د",          "Spa Massage 60min",     "سبا",     "خدمة",  0m, 280m),
                new("TPL-HTL-014", "حمام تركي",          "Turkish Bath",          "سبا",     "خدمة",  0m, 180m),
                new("TPL-HTL-015", "بكج عناية يوم",      "Day Spa Package",       "سبا",     "حزمة",  0m, 850m),
                new("TPL-HTL-016", "غسيل قطعة",          "Per-piece Laundry",     "مغسلة",   "قطعة",  0m,  10m),
                new("TPL-HTL-017", "كي قطعة",            "Per-piece Ironing",     "مغسلة",   "قطعة",  0m,   8m),
                new("TPL-HTL-018", "غسيل عاجل (24س)",    "Express Laundry",       "مغسلة",   "خدمة",  0m,  35m),
            },
            new AcctRow[] {
                new("5361", "تدبير منزلي",     "Housekeeping",   AccountType.Expense),
                new("5362", "مفروشات وغسيل",   "Linen & Laundry",AccountType.Expense),
                new("5363", "كهرباء وماء",     "Utilities",      AccountType.Expense),
                new("5364", "أجور موظفي الفندق","Hotel Wages",   AccountType.Expense),
            }),

        IndustryTemplate.RealEstate => new SeedSet(
            new CatRow[] {
                new("إيجار سكني",   "Residential Lease"),
                new("إيجار تجاري",  "Commercial Lease"),
                new("وساطة",        "Brokerage"),
                new("صيانة",        "Maintenance"),
                new("خدمات",        "Utilities"),
            },
            new ProdRow[] {
                new("TPL-RES-001", "شقة غرفة + صالة",    "Studio Apt",         "إيجار سكني",  "شهر", 0m, 1800m),
                new("TPL-RES-002", "شقة غرفتين",         "1BR Apt",            "إيجار سكني",  "شهر", 0m, 2500m),
                new("TPL-RES-003", "شقة 3 غرف",          "2BR Apt",            "إيجار سكني",  "شهر", 0m, 3500m),
                new("TPL-RES-004", "فيلا 4 غرف",         "4BR Villa",          "إيجار سكني",  "شهر", 0m, 9000m),
                new("TPL-RES-005", "محل تجاري صغير",      "Small Retail Unit",  "إيجار تجاري", "شهر", 0m, 4500m),
                new("TPL-RES-006", "مكتب 50 م²",         "50sqm Office",       "إيجار تجاري", "شهر", 0m, 4000m),
                new("TPL-RES-007", "مستودع 200 م²",      "200sqm Warehouse",   "إيجار تجاري", "شهر", 0m, 6500m),
                new("TPL-RES-008", "عمولة بيع 2.5%",     "Sale Commission 2.5%","وساطة",      "صفقة", 0m,1m),
                new("TPL-RES-009", "عمولة تأجير شهر",     "Lease Commission",   "وساطة",      "صفقة", 0m,1m),
                new("TPL-RES-010", "صيانة دورية",        "Routine Maintenance","صيانة",      "زيارة",0m, 350m),
                new("TPL-RES-011", "صيانة طارئة",        "Emergency Maintenance","صيانة",    "زيارة",0m, 600m),
                new("TPL-RES-012", "خدمات مرافق",        "Utility Service",    "خدمات",      "شهر", 0m, 250m),
            },
            new AcctRow[] {
                new("4361", "إيرادات الإيجار",    "Rent Income",         AccountType.Revenue),
                new("4362", "عمولات الوكلاء",     "Agent Commissions",   AccountType.Revenue),
                new("5365", "تكلفة الصيانة",      "Maintenance Costs",   AccountType.Expense),
                new("5366", "ضرائب وعقارات",       "Property Taxes",      AccountType.Expense),
            }),

        IndustryTemplate.Services => new SeedSet(
            new CatRow[] {
                new("استشارات",   "Consulting"),
                new("صيانة",      "Maintenance"),
                new("تدريب",      "Training"),
                new("تصميم",      "Design"),
                new("دعم فني",    "Technical Support"),
                new("تدقيق",      "Audit"),
            },
            new ProdRow[] {
                new("TPL-SRV-001", "ساعة استشارية",      "Consulting Hour",       "استشارات", "ساعة",  0m,  250m),
                new("TPL-SRV-002", "استشارة استراتيجية",  "Strategy Consult",      "استشارات", "جلسة",  0m, 1500m),
                new("TPL-SRV-003", "زيارة صيانة",        "Maintenance Visit",     "صيانة",    "زيارة", 0m,  350m),
                new("TPL-SRV-004", "صيانة شهرية",        "Monthly Maintenance",   "صيانة",    "شهر",   0m,  800m),
                new("TPL-SRV-005", "جلسة تدريب",         "Training Session",      "تدريب",    "جلسة",  0m,  500m),
                new("TPL-SRV-006", "ورشة عمل يوم كامل",   "Full-day Workshop",     "تدريب",    "يوم",   0m, 3500m),
                new("TPL-SRV-007", "تصميم شعار",         "Logo Design",           "تصميم",    "تصميم", 0m, 1500m),
                new("TPL-SRV-008", "هوية بصرية كاملة",    "Brand Identity Pack",   "تصميم",    "حزمة",  0m, 5500m),
                new("TPL-SRV-009", "تصميم موقع ويب",     "Website Design",        "تصميم",    "موقع",  0m, 8500m),
                new("TPL-SRV-010", "دعم فني عن بعد",     "Remote Support Hour",   "دعم فني",  "ساعة",  0m,  180m),
                new("TPL-SRV-011", "اشتراك دعم شهري",    "Monthly Support Plan",  "دعم فني",  "شهر",   0m, 1200m),
                new("TPL-SRV-012", "تدقيق سنوي",         "Annual Audit",          "تدقيق",    "تدقيق", 0m, 8500m),
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
                new("أدوات",             "Tools"),
            },
            new ProdRow[] {
                new("TPL-CON-001", "إسمنت 50 كجم",       "Cement 50kg",          "مواد البناء",   "كيس",  18m,  28m),
                new("TPL-CON-002", "حديد تسليح 12 مم",   "Rebar 12mm",           "مواد البناء",   "متر",   6m,  10m),
                new("TPL-CON-003", "حديد تسليح 16 مم",   "Rebar 16mm",           "مواد البناء",   "متر",  10m,  16m),
                new("TPL-CON-004", "رمل مغسول",          "Washed Sand",          "مواد البناء",   "م³",   35m,  60m),
                new("TPL-CON-005", "حصى",                "Gravel",               "مواد البناء",   "م³",   45m,  85m),
                new("TPL-CON-006", "بلاط بورسلين",       "Porcelain Tiles",      "مواد البناء",   "م²",   30m,  65m),
                new("TPL-CON-007", "دهان داخلي 18 لتر",  "Interior Paint 18L",   "مواد البناء",   "علبة", 90m, 175m),
                new("TPL-CON-008", "دهان خارجي 18 لتر",  "Exterior Paint 18L",   "مواد البناء",   "علبة",110m, 210m),
                new("TPL-CON-009", "بلوك خرساني",        "Concrete Blocks",      "مواد البناء",   "حبة",   2m,    4m),
                new("TPL-CON-010", "خلاطة إسمنت",        "Cement Mixer",         "معدات",         "وحدة",1500m,3000m),
                new("TPL-CON-011", "سقالة معدنية",       "Steel Scaffold",       "معدات",         "م²",  150m, 280m),
                new("TPL-CON-012", "هزاز خرسانة",        "Concrete Vibrator",    "معدات",         "وحدة", 800m,1600m),
                new("TPL-CON-013", "ساعة عامل بناء",     "Construction Labor Hr","عمالة",         "ساعة", 20m,  35m),
                new("TPL-CON-014", "ساعة كهربائي",       "Electrician Hour",     "عمالة",         "ساعة", 35m,  60m),
                new("TPL-CON-015", "ساعة سباك",          "Plumber Hour",         "عمالة",         "ساعة", 30m,  55m),
                new("TPL-CON-016", "ساعة حداد",          "Steel Worker Hour",    "عمالة",         "ساعة", 32m,  58m),
                new("TPL-CON-017", "م.ف. بلاط",          "Tiling Subcontract",   "مقاولين فرعيين","م²",   35m,  60m),
                new("TPL-CON-018", "م.ف. كهرباء",        "Electrical Subcontract","مقاولين فرعيين","عقد",1m,    1m),
                new("TPL-CON-019", "خوذة عمل",           "Hard Hat",             "أدوات",         "قطعة", 25m,  45m),
                new("TPL-CON-020", "حذاء سلامة",         "Safety Boots",         "أدوات",         "زوج",  90m, 175m),
            },
            new AcctRow[] {
                new("5401", "تكاليف المواد",        "Material Costs",         AccountType.Expense),
                new("5402", "أجور العمال",          "Labor Costs",            AccountType.Expense),
                new("5403", "تكاليف المعدات",       "Equipment Costs",        AccountType.Expense),
                new("5404", "مقاولين فرعيين",       "Subcontractor Costs",    AccountType.Expense),
            }),

        IndustryTemplate.EcommerceStore => new SeedSet(
            new CatRow[] {
                new("ملابس",         "Apparel"),
                new("إلكترونيات",    "Electronics"),
                new("منزلية",        "Home"),
                new("تجميل",         "Beauty"),
                new("أطفال",         "Kids"),
                new("هدايا",         "Gifts"),
            },
            new ProdRow[] {
                new("TPL-ECM-001", "تيشرت رجالي",         "Men's T-shirt",       "ملابس",       "قطعة", 25m,  79m),
                new("TPL-ECM-002", "بنطلون رياضي",        "Joggers",             "ملابس",       "قطعة", 45m, 119m),
                new("TPL-ECM-003", "فستان نسائي",         "Women's Dress",       "ملابس",       "قطعة", 95m, 249m),
                new("TPL-ECM-004", "حقيبة يد",             "Handbag",             "ملابس",       "قطعة",110m, 299m),
                new("TPL-ECM-005", "حذاء رياضي",          "Sneakers",            "ملابس",       "زوج", 130m, 329m),
                new("TPL-ECM-006", "سماعة لاسلكية",        "Wireless Earbuds",    "إلكترونيات",  "قطعة", 80m, 199m),
                new("TPL-ECM-007", "ساعة ذكية",            "Smart Watch",         "إلكترونيات",  "قطعة",250m, 599m),
                new("TPL-ECM-008", "بور بانك 10000",      "Power Bank 10000",    "إلكترونيات",  "قطعة", 50m, 119m),
                new("TPL-ECM-009", "كاميرا أكشن",         "Action Camera",       "إلكترونيات",  "قطعة",350m, 799m),
                new("TPL-ECM-010", "سماعة بلوتوث",        "Bluetooth Speaker",   "إلكترونيات",  "قطعة",100m, 249m),
                new("TPL-ECM-011", "شراشف سرير قطن",       "Cotton Bed Sheets",   "منزلية",      "طقم",  85m, 199m),
                new("TPL-ECM-012", "مخدة طبية",            "Memory Foam Pillow",  "منزلية",      "قطعة", 60m, 149m),
                new("TPL-ECM-013", "إناء طبخ",             "Cooking Pot",         "منزلية",      "قطعة",110m, 249m),
                new("TPL-ECM-014", "مكنسة لاسلكية",        "Cordless Vacuum",     "منزلية",      "قطعة",380m, 849m),
                new("TPL-ECM-015", "آلة قهوة منزلية",     "Home Coffee Machine", "منزلية",      "قطعة",260m, 599m),
                new("TPL-ECM-016", "كريم وجه ترطيب",       "Hydrating Face Cream","تجميل",       "قطعة", 60m, 139m),
                new("TPL-ECM-017", "مسحوق غسول وجه",       "Face Cleansing Powder","تجميل",      "قطعة", 35m,  85m),
                new("TPL-ECM-018", "زيت شعر طبيعي",        "Natural Hair Oil",    "تجميل",       "زجاجة",40m, 95m),
                new("TPL-ECM-019", "عطر فاخر للنساء",      "Premium Women Perfume","تجميل",      "زجاجة",220m,499m),
                new("TPL-ECM-020", "ميك أب كيت",          "Makeup Kit",          "تجميل",       "علبة",180m, 399m),
                new("TPL-ECM-021", "لعبة تركيب أطفال",      "Kids Building Set",   "أطفال",       "علبة", 90m, 199m),
                new("TPL-ECM-022", "لعبة تعليمية",          "Educational Toy",     "أطفال",       "قطعة", 60m, 139m),
                new("TPL-ECM-023", "ملابس أطفال",           "Kids Outfit",         "أطفال",       "طقم",  85m, 199m),
                new("TPL-ECM-024", "حقيبة مدرسية",          "School Backpack",     "أطفال",       "قطعة", 90m, 199m),
                new("TPL-ECM-025", "علبة هدايا فاخرة",      "Premium Gift Box",    "هدايا",       "علبة",250m, 599m),
                new("TPL-ECM-026", "بطاقة هدية 100",        "Gift Card 100",       "هدايا",       "بطاقة",95m, 100m),
                new("TPL-ECM-027", "بطاقة هدية 500",        "Gift Card 500",       "هدايا",       "بطاقة",475m,500m),
                new("TPL-ECM-028", "زهور مع رسالة",          "Flowers with Note",   "هدايا",       "حزمة",100m, 249m),
                new("TPL-ECM-029", "تغليف هدايا",            "Gift Wrapping",       "هدايا",       "خدمة",  5m,  15m),
                new("TPL-ECM-030", "خدمة تخصيص",             "Customization",       "هدايا",       "خدمة", 25m,  60m),
            },
            new AcctRow[] {
                new("5371", "رسوم الشحن",            "Shipping Costs",       AccountType.Expense),
                new("1371", "عهدة الكاش عند الاستلام","COD Float",            AccountType.Asset),
                new("5372", "رسوم بوابات الدفع",     "Payment Gateway Fees", AccountType.Expense),
                new("5373", "مرتجعات وإلغاءات",      "Returns & Refunds",    AccountType.Expense),
            }),

        _ => throw new ArgumentOutOfRangeException(nameof(template), template, null)
    };
}

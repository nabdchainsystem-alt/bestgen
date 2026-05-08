using bestgen.Data;
using bestgen.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Controllers;

[Authorize(Roles = "Owner,Admin")]
public class SettingsController : Controller
{
    private readonly ApplicationDbContext _context;

    public SettingsController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var settings = await _context.CompanySettings.AsNoTracking().FirstOrDefaultAsync();
        return View(settings ?? new CompanySettings());
    }

    public async Task<IActionResult> Edit()
    {
        var settings = await _context.CompanySettings.FirstOrDefaultAsync();
        return View(settings ?? new CompanySettings());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CompanySettings model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (model.Id == 0)
        {
            _context.CompanySettings.Add(model);
        }
        else
        {
            _context.CompanySettings.Update(model);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public IActionResult UsersRoles()
    {
        return View();
    }

    public IActionResult TaxSettings() =>
        View("Placeholder", new PlaceholderViewModel("إعدادات الضريبة",
            "إدارة نسبة ضريبة القيمة المضافة الافتراضية، الرقم الضريبي، وجاهزية الفوترة الإلكترونية ZATCA.",
            new[] { "نسبة الضريبة الافتراضية", "الرقم الضريبي", "تفعيل الفوترة الإلكترونية", "جاهزية ZATCA Phase 2" }));

    public IActionResult InvoiceSettings() =>
        View("Placeholder", new PlaceholderViewModel("إعدادات الفواتير",
            "ضبط بادئات أرقام الفواتير، الترقيم التلقائي، وقالب الفاتورة وتذييلها.",
            new[] { "بادئة فاتورة المبيعات", "بادئة فاتورة الشراء", "بادئة عرض السعر", "ترقيم تلقائي", "تذييل الفاتورة", "قالب الطباعة" }));

    public IActionResult Branches() =>
        View("Placeholder", new PlaceholderViewModel("الفروع",
            "إدارة فروع المنشأة وربطها بالمستودعات وأرقام التسلسل.",
            new[] { "إضافة فرع", "ربط بالمستودعات", "تسلسل أرقام مخصص", "حالة الفرع" }));

    public IActionResult NumberingSettings() =>
        View("Placeholder", new PlaceholderViewModel("إعدادات الترقيم",
            "تخصيص أنماط ترقيم الفواتير والإيصالات والقيود تلقائيا.",
            new[] { "نمط الترقيم", "بادئات حسب الفرع", "إعادة تشغيل سنوي", "حد الأرقام المتاح" }));

    public IActionResult CurrencySettings() =>
        View("Placeholder", new PlaceholderViewModel("إعدادات العملات",
            "العملة الأساسية وعملات إضافية وأسعار الصرف عند الحاجة.",
            new[] { "العملة الأساسية", "عملات إضافية", "أسعار الصرف", "تحديث تلقائي للسعر" }));

    public IActionResult FutureIntegrations() =>
        View("Placeholder", new PlaceholderViewModel("التكاملات المستقبلية",
            "ربط النظام بمنصات الدفع، البنوك، WhatsApp، ZATCA، وأنظمة التوصيل.",
            new[] { "بوابات الدفع", "ربط البنوك", "WhatsApp Business", "ZATCA Phase 2", "أنظمة التوصيل", "بريد إلكتروني تلقائي" }));
}

public record PlaceholderViewModel(string Title, string Description, IReadOnlyList<string> Items);

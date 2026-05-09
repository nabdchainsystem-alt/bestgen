using bestgen.Data;
using bestgen.Models;
using bestgen.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Controllers;

[Authorize(Roles = "Owner,Admin")]
public class SettingsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IWebHostEnvironment _env;

    public SettingsController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IWebHostEnvironment env)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _env = env;
    }

    private static bool IsArabic => System.Globalization.CultureInfo.CurrentUICulture.Name
        .StartsWith("ar", StringComparison.OrdinalIgnoreCase);

    private static string T(string en, string ar) => IsArabic ? ar : en;

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
    public async Task<IActionResult> Edit(CompanySettings model, IFormFile? logo)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (logo is { Length: > 0 })
        {
            model.LogoPath = await SaveLogoAsync(logo);
        }

        await UpsertSettingsAsync(model);
        TempData["SettingsMessage"] = T("Company profile saved.", "تم حفظ ملف الشركة.");
        return RedirectToAction(nameof(Index));
    }

    // ---- Tax settings ----

    public async Task<IActionResult> TaxSettings()
    {
        return View(await GetOrInitSettingsAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TaxSettings(CompanySettings model)
    {
        var current = await GetOrInitSettingsAsync(track: true);
        current.DefaultVatRate = model.DefaultVatRate;
        current.VatNumber = model.VatNumber;
        current.ZatcaEnabled = model.ZatcaEnabled;
        current.ZatcaTaxpayerId = model.ZatcaTaxpayerId;
        current.ShowInvoiceQr = model.ShowInvoiceQr;
        await _context.SaveChangesAsync();
        TempData["SettingsMessage"] = T("VAT settings saved.", "تم حفظ إعدادات الضريبة.");
        return RedirectToAction(nameof(TaxSettings));
    }

    // ---- Invoice settings ----

    public async Task<IActionResult> InvoiceSettings()
    {
        return View(await GetOrInitSettingsAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InvoiceSettings(CompanySettings model)
    {
        var current = await GetOrInitSettingsAsync(track: true);
        current.InvoicePrefix = model.InvoicePrefix;
        current.PurchaseInvoicePrefix = model.PurchaseInvoicePrefix;
        current.InvoiceFooterAr = model.InvoiceFooterAr;
        current.InvoiceFooterEn = model.InvoiceFooterEn;
        current.ShowInvoiceQr = model.ShowInvoiceQr;
        await _context.SaveChangesAsync();
        TempData["SettingsMessage"] = T("Invoice settings saved.", "تم حفظ إعدادات الفواتير.");
        return RedirectToAction(nameof(InvoiceSettings));
    }

    // ---- Currency settings ----

    public async Task<IActionResult> CurrencySettings()
    {
        return View(await GetOrInitSettingsAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CurrencySettings(CompanySettings model)
    {
        var current = await GetOrInitSettingsAsync(track: true);
        current.BaseCurrency = string.IsNullOrWhiteSpace(model.BaseCurrency) ? "SAR" : model.BaseCurrency.ToUpperInvariant();
        current.CurrencySymbol = string.IsNullOrWhiteSpace(model.CurrencySymbol) ? "ر.س" : model.CurrencySymbol;
        await _context.SaveChangesAsync();
        TempData["SettingsMessage"] = T("Currency settings saved.", "تم حفظ إعدادات العملة.");
        return RedirectToAction(nameof(CurrencySettings));
    }

    // ---- Numbering settings ----

    public async Task<IActionResult> NumberingSettings()
    {
        var policies = await _context.NumberingPolicies
            .AsNoTracking()
            .OrderBy(x => x.DocumentType)
            .ToListAsync();
        return View(policies);
    }

    // ---- Branches & integrations ----

    public IActionResult Branches() => RedirectToAction("Index", "Branches");

    public IActionResult FutureIntegrations() => View();

    // ---- Users & roles (Identity) ----

    public async Task<IActionResult> UsersRoles()
    {
        var users = await _userManager.Users.AsNoTracking().OrderBy(u => u.Email).ToListAsync();
        var rows = new List<UserRowViewModel>(users.Count);
        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            rows.Add(new UserRowViewModel
            {
                Id = u.Id,
                Email = u.Email ?? "",
                FullName = u.FullName,
                Roles = roles,
                IsLocked = u.LockoutEnd is { } end && end > DateTimeOffset.UtcNow
            });
        }

        ViewBag.AllRoles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();
        return View(rows);
    }

    public async Task<IActionResult> CreateUser()
    {
        ViewBag.AllRoles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();
        return View(new UserFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(UserFormViewModel model)
    {
        ViewBag.AllRoles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();

        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(model.Password))
        {
            if (string.IsNullOrWhiteSpace(model.Password))
            {
                ModelState.AddModelError(nameof(model.Password), T("Password is required.", "كلمة المرور مطلوبة."));
            }
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName,
            EmailConfirmed = true
        };

        var create = await _userManager.CreateAsync(user, model.Password);
        if (!create.Succeeded)
        {
            foreach (var err in create.Errors) ModelState.AddModelError("", err.Description);
            return View(model);
        }

        if (model.Roles is { Count: > 0 })
        {
            await _userManager.AddToRolesAsync(user, model.Roles);
        }

        TempData["SettingsMessage"] = T($"User {user.Email} was created.", $"تم إنشاء المستخدم {user.Email}.");
        return RedirectToAction(nameof(UsersRoles));
    }

    public async Task<IActionResult> EditUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        ViewBag.AllRoles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();

        return View(new UserFormViewModel
        {
            Id = user.Id,
            Email = user.Email ?? "",
            FullName = user.FullName,
            Roles = (await _userManager.GetRolesAsync(user)).ToList()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUser(UserFormViewModel model)
    {
        var user = await _userManager.FindByIdAsync(model.Id ?? "");
        if (user is null) return NotFound();

        ViewBag.AllRoles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();

        user.FullName = model.FullName;
        user.Email = model.Email;
        user.UserName = model.Email;
        var update = await _userManager.UpdateAsync(user);
        if (!update.Succeeded)
        {
            foreach (var err in update.Errors) ModelState.AddModelError("", err.Description);
            return View(model);
        }

        var current = await _userManager.GetRolesAsync(user);
        var desired = model.Roles ?? new List<string>();
        var toRemove = current.Except(desired).ToList();
        var toAdd = desired.Except(current).ToList();
        if (toRemove.Count > 0) await _userManager.RemoveFromRolesAsync(user, toRemove);
        if (toAdd.Count > 0) await _userManager.AddToRolesAsync(user, toAdd);

        if (!string.IsNullOrWhiteSpace(model.Password))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var reset = await _userManager.ResetPasswordAsync(user, token, model.Password);
            if (!reset.Succeeded)
            {
                foreach (var err in reset.Errors) ModelState.AddModelError("", err.Description);
                return View(model);
            }
        }

        TempData["SettingsMessage"] = T($"User {user.Email} was updated.", $"تم تحديث المستخدم {user.Email}.");
        return RedirectToAction(nameof(UsersRoles));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        var currentEmail = User.Identity?.Name;
        if (string.Equals(currentEmail, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            TempData["SettingsMessage"] = T("You can't delete the account you're currently signed in with.", "لا يمكن حذف الحساب الذي تستخدمه حاليا.");
            return RedirectToAction(nameof(UsersRoles));
        }

        await _userManager.DeleteAsync(user);
        TempData["SettingsMessage"] = T("User deleted.", "تم حذف المستخدم.");
        return RedirectToAction(nameof(UsersRoles));
    }

    // ---- helpers ----

    private async Task<CompanySettings> GetOrInitSettingsAsync(bool track = false)
    {
        var query = track ? _context.CompanySettings.AsQueryable() : _context.CompanySettings.AsNoTracking();
        var existing = await query.FirstOrDefaultAsync();
        if (existing is not null) return existing;

        var fresh = new CompanySettings { CompanyNameAr = "Bestgen" };
        if (track)
        {
            _context.CompanySettings.Add(fresh);
            await _context.SaveChangesAsync();
        }
        return fresh;
    }

    private async Task UpsertSettingsAsync(CompanySettings model)
    {
        if (model.Id == 0) _context.CompanySettings.Add(model);
        else _context.CompanySettings.Update(model);
        await _context.SaveChangesAsync();
    }

    private async Task<string> SaveLogoAsync(IFormFile file)
    {
        var uploads = Path.Combine(_env.WebRootPath, "uploads");
        Directory.CreateDirectory(uploads);
        var safeName = $"logo-{DateTime.UtcNow:yyyyMMddHHmmss}{Path.GetExtension(file.FileName)}";
        var fullPath = Path.Combine(uploads, safeName);
        using var stream = System.IO.File.Create(fullPath);
        await file.CopyToAsync(stream);
        return $"/uploads/{safeName}";
    }
}


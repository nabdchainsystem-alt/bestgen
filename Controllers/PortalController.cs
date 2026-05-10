using System.Security.Claims;
using System.Security.Cryptography;
using bestgen.Data;
using bestgen.Models;
using bestgen.Services.InvoicePdf;
using bestgen.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Controllers;

[Route("portal/{action=Index}/{id?}")]
public class PortalController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly InvoicePdfService _pdf;

    public PortalController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn,
        InvoicePdfService pdf)
    {
        _db = db;
        _users = users;
        _signIn = signIn;
        _pdf = pdf;
    }

    // ----- Login -----

    [HttpGet, AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
    {
        var result = await _signIn.PasswordSignInAsync(email, password, isPersistent: true, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }
        var user = await _users.FindByEmailAsync(email);
        if (user is null) return RedirectToAction(nameof(Login));
        var roles = await _users.GetRolesAsync(user);
        if (!roles.Contains("Customer"))
        {
            // Staff hit the portal login by mistake — send them to the workspace.
            return Redirect("/");
        }
        return Redirect(returnUrl ?? Url.Action(nameof(Index))!);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    // ----- Register from invitation -----

    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> Register(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return BadRequest("Token required.");
        var inv = await _db.CustomerInvitations.IgnoreQueryFilters()
            .Include(i => i.Customer)
            .FirstOrDefaultAsync(i => i.Token == token);
        if (inv is null) return NotFound();
        if (inv.UsedAt is not null) { ViewBag.Used = true; return View("RegisterError"); }
        if (inv.ExpiresAt < DateTime.UtcNow) { ViewBag.Expired = true; return View("RegisterError"); }
        ViewBag.Email = inv.Email;
        ViewBag.CustomerName = inv.Customer?.NameAr ?? inv.Customer?.NameEn ?? inv.Email;
        ViewBag.Token = token;
        return View();
    }

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(string token, string password, string fullName)
    {
        var inv = await _db.CustomerInvitations.IgnoreQueryFilters()
            .Include(i => i.Customer)
            .FirstOrDefaultAsync(i => i.Token == token);
        if (inv is null || inv.UsedAt is not null || inv.ExpiresAt < DateTime.UtcNow)
        {
            return RedirectToAction(nameof(RegisterError));
        }

        var tenantId = _db.Entry(inv).Property<int>("TenantId").CurrentValue;

        var existing = await _users.FindByEmailAsync(inv.Email);
        if (existing is not null)
        {
            ModelState.AddModelError(string.Empty, "An account already exists for this email.");
            ViewBag.Email = inv.Email; ViewBag.Token = token; ViewBag.CustomerName = inv.Customer?.NameAr;
            return View();
        }

        var user = new ApplicationUser
        {
            UserName = inv.Email,
            Email = inv.Email,
            EmailConfirmed = true,
            FullName = string.IsNullOrWhiteSpace(fullName) ? inv.Email : fullName.Trim(),
            CurrentTenantId = tenantId,
            CustomerId = inv.CustomerId
        };
        var create = await _users.CreateAsync(user, password);
        if (!create.Succeeded)
        {
            foreach (var err in create.Errors) ModelState.AddModelError(string.Empty, err.Description);
            ViewBag.Email = inv.Email; ViewBag.Token = token; ViewBag.CustomerName = inv.Customer?.NameAr;
            return View();
        }
        await _users.AddToRoleAsync(user, "Customer");

        inv.UsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _signIn.SignInAsync(user, isPersistent: true);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet, AllowAnonymous]
    public IActionResult RegisterError() => View();

    // ----- Authenticated portal pages (Customer role only) -----

    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> Index()
    {
        var customerId = await CurrentCustomerIdAsync();
        if (customerId is null) return RedirectToAction(nameof(Login));

        var outstanding = await _db.SalesInvoices.AsNoTracking()
            .Where(i => i.CustomerId == customerId && i.RemainingAmount > 0)
            .SumAsync(i => (decimal?)i.RemainingAmount) ?? 0m;
        var recentInvoices = await _db.SalesInvoices.AsNoTracking()
            .Where(i => i.CustomerId == customerId)
            .OrderByDescending(i => i.InvoiceDate)
            .Take(5).ToListAsync();
        var openQuotations = await _db.SalesQuotations.AsNoTracking()
            .Where(q => q.CustomerId == customerId && q.Status != QuotationStatus.Accepted && q.Status != QuotationStatus.Rejected)
            .CountAsync();

        ViewBag.Outstanding = outstanding;
        ViewBag.RecentInvoices = recentInvoices;
        ViewBag.OpenQuotations = openQuotations;
        ViewBag.Customer = await _db.Customers.FindAsync(customerId);
        return View();
    }

    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> Invoices()
    {
        var customerId = await CurrentCustomerIdAsync();
        if (customerId is null) return RedirectToAction(nameof(Login));
        var rows = await _db.SalesInvoices.AsNoTracking()
            .Where(i => i.CustomerId == customerId)
            .OrderByDescending(i => i.InvoiceDate)
            .ToListAsync();
        return View(rows);
    }

    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> InvoiceDetail(int id)
    {
        var customerId = await CurrentCustomerIdAsync();
        if (customerId is null) return RedirectToAction(nameof(Login));
        var inv = await _db.SalesInvoices.AsNoTracking()
            .Include(i => i.Items).ThenInclude(it => it.Product)
            .FirstOrDefaultAsync(i => i.Id == id && i.CustomerId == customerId);
        if (inv is null) return NotFound();
        return View(inv);
    }

    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> InvoicePdf(int id)
    {
        var customerId = await CurrentCustomerIdAsync();
        if (customerId is null) return Unauthorized();
        // Verify ownership before rendering — query filter doesn't help with cross-tenant guard here.
        var owns = await _db.SalesInvoices.AsNoTracking()
            .AnyAsync(i => i.Id == id && i.CustomerId == customerId);
        if (!owns) return Forbid();
        var pdf = await _pdf.RenderSalesInvoiceAsync(id);
        if (pdf is null) return NotFound();
        return File(pdf.Value.Bytes, "application/pdf", pdf.Value.FileName);
    }

    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> Quotations()
    {
        var customerId = await CurrentCustomerIdAsync();
        if (customerId is null) return RedirectToAction(nameof(Login));
        var rows = await _db.SalesQuotations.AsNoTracking()
            .Where(q => q.CustomerId == customerId)
            .OrderByDescending(q => q.QuotationDate)
            .ToListAsync();
        return View(rows);
    }

    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> QuotationDetail(int id)
    {
        var customerId = await CurrentCustomerIdAsync();
        if (customerId is null) return RedirectToAction(nameof(Login));
        var q = await _db.SalesQuotations.AsNoTracking()
            .Include(x => x.Items).ThenInclude(it => it.Product)
            .FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == customerId);
        if (q is null) return NotFound();
        return View(q);
    }

    [HttpPost, Authorize(Roles = "Customer"), ValidateAntiForgeryToken]
    public async Task<IActionResult> RespondQuotation(int id, string action)
    {
        var customerId = await CurrentCustomerIdAsync();
        if (customerId is null) return RedirectToAction(nameof(Login));
        var q = await _db.SalesQuotations.FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == customerId);
        if (q is null) return NotFound();
        q.Status = action switch
        {
            "accept" => QuotationStatus.Accepted,
            "reject" => QuotationStatus.Rejected,
            _ => q.Status
        };
        q.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["PortalMessage"] = action == "accept"
            ? "Quotation accepted — the team will follow up with the invoice."
            : "Quotation rejected.";
        return RedirectToAction(nameof(QuotationDetail), new { id });
    }

    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> Profile()
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return RedirectToAction(nameof(Login));
        return View(user);
    }

    [HttpPost, Authorize(Roles = "Customer"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(string fullName, string? phoneNumber)
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return RedirectToAction(nameof(Login));
        user.FullName = string.IsNullOrWhiteSpace(fullName) ? user.FullName : fullName.Trim();
        user.PhoneNumber = phoneNumber?.Trim();
        await _users.UpdateAsync(user);
        TempData["PortalMessage"] = "Profile updated.";
        return RedirectToAction(nameof(Profile));
    }

    private async Task<int?> CurrentCustomerIdAsync()
    {
        var user = await _users.GetUserAsync(User);
        return user?.CustomerId;
    }

    // Helper for staff-side controllers — generates a random invitation token.
    public static string GenerateToken()
    {
        var bytes = new byte[24];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}

using bestgen.Data;
using bestgen.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Controllers;

/// <summary>
/// Staff-side admin page that issues / revokes customer-portal invitations.
/// Each row in the table is a customer; click "Invite" to generate a one-time
/// URL the customer follows to set their password and access /portal.
/// </summary>
[Authorize(Roles = "Owner,Admin,Sales")]
public class PortalInvitationsController : Controller
{
    private readonly ApplicationDbContext _db;

    public PortalInvitationsController(ApplicationDbContext db) { _db = db; }

    public async Task<IActionResult> Index()
    {
        // Customers with the most-recent invitation status (if any) joined client-side.
        var customers = await _db.Customers.AsNoTracking()
            .OrderBy(c => c.NameAr)
            .ToListAsync();
        var invitations = await _db.CustomerInvitations.AsNoTracking()
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();
        ViewBag.Invitations = invitations
            .GroupBy(i => i.CustomerId)
            .ToDictionary(g => g.Key, g => g.First());
        return View(customers);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate(int customerId, string email)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == customerId);
        if (customer is null)
        {
            TempData["InvitationError"] = "Customer not found.";
            return RedirectToAction(nameof(Index));
        }
        if (string.IsNullOrWhiteSpace(email))
        {
            email = customer.Email ?? "";
        }
        if (string.IsNullOrWhiteSpace(email))
        {
            TempData["InvitationError"] = "Customer has no email — pass one explicitly.";
            return RedirectToAction(nameof(Index));
        }

        var token = PortalController.GenerateToken();
        _db.CustomerInvitations.Add(new CustomerInvitation
        {
            CustomerId = customer.Id,
            Email = email.Trim(),
            Token = token,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(14),
            CreatedByUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        });
        await _db.SaveChangesAsync();

        var url = Url.Action("Register", "Portal", new { token }, Request.Scheme);
        TempData["InvitationLink"] = url;
        TempData["InvitationCustomer"] = customer.NameAr;
        TempData["InvitationEmail"] = email;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke(int id)
    {
        var inv = await _db.CustomerInvitations.FirstOrDefaultAsync(i => i.Id == id);
        if (inv is not null && inv.UsedAt is null)
        {
            inv.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}

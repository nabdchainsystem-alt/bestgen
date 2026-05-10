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
    [bestgen.Services.Authorization.RequirePermission("portal.invite")]
    public async Task<IActionResult> Generate(int customerId, string email,
        [FromServices] bestgen.Services.Delivery.EmailDeliveryService emailService,
        [FromServices] IConfiguration config)
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

        var url = Url.Action("Register", "Portal", new { token }, Request.Scheme) ?? "";
        var customerLabel = string.IsNullOrWhiteSpace(customer.NameEn) ? customer.NameAr : customer.NameEn;

        // If SMTP is configured, fire-and-forget the invitation email.
        if (emailService.IsConfigured)
        {
            var senderName = config["Smtp:FromName"] ?? "Bestgen";
            var subject = $"You're invited to {senderName}'s portal";
            var body =
                $"Hello {customerLabel},\n\n" +
                $"{senderName} has invited you to their customer portal where you can view your invoices, quotations, and outstanding balance.\n\n" +
                $"Activate your portal access here (link expires in 14 days):\n{url}\n\n" +
                $"If you didn't expect this invitation, you can safely ignore this email.\n\n" +
                $"— {senderName}";

            var (ok, err, _) = await emailService.SendAsync(email.Trim(), subject, body);
            if (ok)
            {
                TempData["InvitationEmailed"] = $"Invitation emailed to {email}.";
            }
            else
            {
                TempData["InvitationError"] = $"Email send failed: {err}";
            }
        }

        // Always show the link as a fallback (and for SMTP-not-configured case).
        TempData["InvitationLink"] = url;
        TempData["InvitationCustomer"] = customerLabel;
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

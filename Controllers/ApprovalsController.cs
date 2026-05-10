using bestgen.Data;
using bestgen.Models;
using bestgen.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Controllers;

[Authorize]
public class ApprovalsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ApprovalService _approvals;

    public ApprovalsController(ApplicationDbContext db, ApprovalService approvals)
    {
        _db = db;
        _approvals = approvals;
    }

    public async Task<IActionResult> Index()
    {
        var pending = await _approvals.ListPendingAsync();
        var recent = await _approvals.ListRecentAsync();
        var policies = await _approvals.ListPoliciesAsync();
        ViewBag.Pending = pending;
        ViewBag.Recent = recent;
        ViewBag.Policies = policies;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [bestgen.Services.Idempotency.Idempotent]
    [bestgen.Services.Authorization.RequirePermission("approvals.act")]
    public async Task<IActionResult> Approve(int id, string? comment)
    {
        if (await ResolveGuardAsync(id) is { } guard) return guard;
        var (uid, name) = CurrentUser();
        var resolved = await _approvals.ResolveAsync(id, ApprovalStatus.Approved, uid, name, comment);
        if (resolved is null) return NotFound();
        TempData["ApprovalMessage"] = $"Approved {resolved.DocumentNumber}.";
        return RedirectBack(resolved);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [bestgen.Services.Idempotency.Idempotent]
    [bestgen.Services.Authorization.RequirePermission("approvals.act")]
    public async Task<IActionResult> Reject(int id, string? comment)
    {
        if (await ResolveGuardAsync(id) is { } guard) return guard;
        var (uid, name) = CurrentUser();
        var resolved = await _approvals.ResolveAsync(id, ApprovalStatus.Rejected, uid, name, comment);
        if (resolved is null) return NotFound();
        TempData["ApprovalMessage"] = $"Rejected {resolved.DocumentNumber}.";
        return RedirectBack(resolved);
    }

    /// <summary>Returns null when the user is allowed to act, otherwise a Forbid/NotFound result to short-circuit with.</summary>
    private async Task<IActionResult?> ResolveGuardAsync(int id)
    {
        var req = await _db.ApprovalRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
        if (req is null) return NotFound();
        var roles = User.Claims.Where(c => c.Type == System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToList();
        if (!await _approvals.CanResolveAsync(req, roles))
        {
            TempData["ApprovalError"] = "You don't have the required role to act on this request.";
            return RedirectBack(req);
        }
        return null;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [bestgen.Services.Authorization.RequirePermission("approvals.policies")]
    public async Task<IActionResult> SavePolicy(ApprovalPolicy policy)
    {
        if (policy.MinAmount < 0) policy.MinAmount = 0;
        await _approvals.SavePolicyAsync(policy);
        TempData["ApprovalMessage"] = "Policy saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [bestgen.Services.Authorization.RequirePermission("approvals.policies")]
    public async Task<IActionResult> DeletePolicy(int id)
    {
        await _approvals.DeletePolicyAsync(id);
        TempData["ApprovalMessage"] = "Policy deleted.";
        return RedirectToAction(nameof(Index));
    }

    private (string? userId, string? name) CurrentUser()
    {
        var name = User.Identity?.IsAuthenticated == true ? User.Identity!.Name : null;
        var uid = User.FindFirst("sub")?.Value;
        return (uid, name);
    }

    private IActionResult RedirectBack(ApprovalRequest req)
    {
        // If an approval came from an invoice details page, send the user back there.
        var referer = Request.Headers["Referer"].ToString();
        if (!string.IsNullOrEmpty(referer)
            && (referer.Contains("/SalesInvoices/Details", StringComparison.OrdinalIgnoreCase)
                || referer.Contains("/PurchaseInvoices/Details", StringComparison.OrdinalIgnoreCase)))
        {
            return Redirect(referer);
        }
        return RedirectToAction(nameof(Index));
    }
}

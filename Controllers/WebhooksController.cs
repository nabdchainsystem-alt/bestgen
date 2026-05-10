using bestgen.Data;
using bestgen.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Controllers;

[Authorize(Roles = "Owner,Admin")]
public class WebhooksController : Controller
{
    private readonly ApplicationDbContext _db;
    public WebhooksController(ApplicationDbContext db) { _db = db; }

    public async Task<IActionResult> Index()
    {
        var hooks = await _db.Webhooks.AsNoTracking().OrderByDescending(w => w.CreatedAt).ToListAsync();
        var recent = await _db.WebhookDeliveries.AsNoTracking()
            .OrderByDescending(d => d.CreatedAt).Take(50).ToListAsync();
        ViewBag.Recent = recent;
        return View(hooks);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, string url, string events, string? secret)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
        {
            TempData["WebhookError"] = "Name and URL are required.";
            return RedirectToAction(nameof(Index));
        }
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || (u.Scheme != "https" && u.Scheme != "http"))
        {
            TempData["WebhookError"] = "URL must be absolute http/https.";
            return RedirectToAction(nameof(Index));
        }
        _db.Webhooks.Add(new Webhook
        {
            Name = name.Trim(),
            Url = url.Trim(),
            Events = string.IsNullOrWhiteSpace(events) ? "*" : events.Trim(),
            Secret = string.IsNullOrWhiteSpace(secret) ? null : secret.Trim(),
            IsActive = true
        });
        await _db.SaveChangesAsync();
        TempData["WebhookMessage"] = "Webhook registered.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id)
    {
        var w = await _db.Webhooks.FirstOrDefaultAsync(x => x.Id == id);
        if (w is not null)
        {
            w.IsActive = !w.IsActive;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var w = await _db.Webhooks.FirstOrDefaultAsync(x => x.Id == id);
        if (w is not null)
        {
            _db.Webhooks.Remove(w);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}

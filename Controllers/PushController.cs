using System.Security.Claims;
using bestgen.Data;
using bestgen.Models;
using bestgen.Services.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Controllers;

[Authorize]
public class PushController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly WebPushService _push;

    public PushController(ApplicationDbContext db, WebPushService push)
    {
        _db = db;
        _push = push;
    }

    [Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> Index()
    {
        ViewBag.Configured = _push.IsConfigured;
        ViewBag.PublicKey = _push.PublicKey;
        ViewBag.Subject = _push.Subject;
        ViewBag.Subscriptions = await _db.PushSubscriptions.AsNoTracking()
            .OrderByDescending(s => s.CreatedAt).Take(100).ToListAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Owner,Admin")]
    public IActionResult GenerateKeys()
    {
        var (pub, priv) = WebPushService.GenerateKeys();
        TempData["GeneratedPublicKey"] = pub;
        TempData["GeneratedPrivateKey"] = priv;
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Public endpoint so the frontend can read VAPID public key.</summary>
    [HttpGet, AllowAnonymous]
    public IActionResult PublicKey() => Json(new { publicKey = _push.PublicKey, configured = _push.IsConfigured });

    /// <summary>Browser POSTs its subscription here after the user grants permission.</summary>
    [HttpPost]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest req)
    {
        if (req is null || string.IsNullOrEmpty(req.Endpoint) || string.IsNullOrEmpty(req.P256dh) || string.IsNullOrEmpty(req.Auth))
        {
            return BadRequest(new { error = "Invalid subscription." });
        }
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var existing = await _db.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == req.Endpoint);
        if (existing is null)
        {
            _db.PushSubscriptions.Add(new Models.PushSubscription
            {
                UserId = userId,
                Endpoint = req.Endpoint,
                P256dh = req.P256dh,
                Auth = req.Auth,
                UserAgent = Request.Headers["User-Agent"].ToString(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.UserId = userId;
            existing.P256dh = req.P256dh;
            existing.Auth = req.Auth;
            existing.IsActive = true;
        }
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpPost]
    public async Task<IActionResult> Unsubscribe([FromBody] UnsubscribeRequest req)
    {
        if (req is null || string.IsNullOrEmpty(req.Endpoint)) return BadRequest();
        var sub = await _db.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == req.Endpoint);
        if (sub is not null)
        {
            sub.IsActive = false;
            await _db.SaveChangesAsync();
        }
        return Ok(new { ok = true });
    }

    /// <summary>Fires a test notification to the current user.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Test()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        var sent = await _push.SendToUserAsync(userId, new NotificationPayload
        {
            Title = "Bestgen test 🔔",
            Body = "Web Push is working — you'll see this when invoices change too.",
            Url = "/",
            Tag = "bestgen-test"
        });
        TempData["PushMessage"] = sent > 0
            ? $"Test push sent to {sent} subscription(s)."
            : "No active subscriptions found for your account — enable notifications first.";
        return Redirect(Request.Headers["Referer"].ToString() ?? "/");
    }

    public sealed class SubscribeRequest
    {
        public string Endpoint { get; set; } = "";
        public string P256dh { get; set; } = "";
        public string Auth { get; set; } = "";
    }
    public sealed class UnsubscribeRequest { public string Endpoint { get; set; } = ""; }
}

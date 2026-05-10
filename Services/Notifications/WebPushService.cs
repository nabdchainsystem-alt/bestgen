using System.Text.Json;
using bestgen.Data;
using Microsoft.EntityFrameworkCore;
using WebPush;

namespace bestgen.Services.Notifications;

/// <summary>
/// Sends Web Push notifications via VAPID. VAPID keys live in config
/// (<c>WebPush:VapidPublicKey</c> + <c>WebPush:VapidPrivateKey</c>) and are
/// generated once via <see cref="GenerateKeys"/> on the admin page.
/// </summary>
public class WebPushService
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<WebPushService> _logger;

    public WebPushService(ApplicationDbContext db, IConfiguration config, ILogger<WebPushService> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    public string PublicKey => _config["WebPush:VapidPublicKey"] ?? string.Empty;
    public string PrivateKey => _config["WebPush:VapidPrivateKey"] ?? string.Empty;
    public string Subject => _config["WebPush:VapidSubject"] ?? "mailto:admin@bestgen.example";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(PublicKey) && !string.IsNullOrWhiteSpace(PrivateKey);

    /// <summary>Generates a fresh VAPID key pair (curve P-256) — owner pastes them into env vars.</summary>
    public static (string PublicKey, string PrivateKey) GenerateKeys()
    {
        var keys = VapidHelper.GenerateVapidKeys();
        return (keys.PublicKey, keys.PrivateKey);
    }

    public async Task<int> SendToUserAsync(string userId, NotificationPayload payload, CancellationToken ct = default)
    {
        var subs = await _db.PushSubscriptions.AsNoTracking()
            .Where(s => s.UserId == userId && s.IsActive)
            .ToListAsync(ct);
        return await SendManyAsync(subs, payload, ct);
    }

    public async Task<int> SendToAllAsync(NotificationPayload payload, CancellationToken ct = default)
    {
        var subs = await _db.PushSubscriptions.AsNoTracking()
            .Where(s => s.IsActive)
            .ToListAsync(ct);
        return await SendManyAsync(subs, payload, ct);
    }

    public async Task<int> SendManyAsync(IEnumerable<Models.PushSubscription> subs, NotificationPayload payload, CancellationToken ct = default)
    {
        if (!IsConfigured) return 0;
        var vapid = new VapidDetails(Subject, PublicKey, PrivateKey);
        var client = new WebPushClient();
        var json = JsonSerializer.Serialize(payload);
        var sent = 0;
        var dead = new List<int>();

        foreach (var s in subs)
        {
            try
            {
                var sub = new WebPush.PushSubscription(s.Endpoint, s.P256dh, s.Auth);
                await client.SendNotificationAsync(sub, json, vapid, ct);
                sent++;
            }
            catch (WebPushException ex) when ((int)ex.StatusCode == 404 || (int)ex.StatusCode == 410)
            {
                // Browser unsubscribed — kill the subscription.
                dead.Add(s.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Push send failed to subscription {Id}", s.Id);
            }
        }

        if (dead.Count > 0)
        {
            await _db.PushSubscriptions
                .Where(x => dead.Contains(x.Id))
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsActive, false), ct);
        }
        return sent;
    }
}

public sealed class NotificationPayload
{
    public string Title { get; set; } = "Bestgen";
    public string? Body { get; set; }
    public string? Url { get; set; }
    public string? Icon { get; set; } = "/img/logo.png";
    public string? Tag { get; set; }
}

using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using bestgen.Data;
using bestgen.Models;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services.Webhooks;

/// <summary>
/// Dispatches application events to registered webhooks. Each event POSTs JSON
/// to the subscriber URL with an HMAC-SHA256 signature in <c>X-Bestgen-Signature</c>.
/// Failures are recorded as <see cref="WebhookDelivery"/> rows for audit + retry.
/// </summary>
public class WebhookService
{
    private readonly ApplicationDbContext _db;
    private readonly HttpClient _http;
    private readonly ILogger<WebhookService> _logger;

    public WebhookService(ApplicationDbContext db, HttpClient http, ILogger<WebhookService> logger)
    {
        _db = db;
        _http = http;
        _logger = logger;
    }

    /// <summary>Fire-and-forget enqueue (called via Hangfire from event sources).</summary>
    public async Task DispatchAsync(string eventName, object payload, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(new
        {
            @event = eventName,
            timestamp = DateTime.UtcNow,
            data = payload
        });

        var subscribers = await _db.Webhooks.AsNoTracking()
            .Where(w => w.IsActive && (w.Events == "*" || w.Events.Contains(eventName)))
            .ToListAsync(ct);

        foreach (var w in subscribers)
        {
            await SendAsync(w, eventName, json, ct);
        }
    }

    private async Task SendAsync(Webhook w, string eventName, string json, CancellationToken ct)
    {
        var delivery = new WebhookDelivery
        {
            WebhookId = w.Id,
            Event = eventName,
            Payload = json.Length > 4000 ? json[..4000] : json,
            Status = WebhookDeliveryStatus.Queued,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, w.Url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            req.Headers.TryAddWithoutValidation("X-Bestgen-Event", eventName);
            req.Headers.TryAddWithoutValidation("User-Agent", "Bestgen-Webhook/1.0");

            if (!string.IsNullOrEmpty(w.Secret))
            {
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(w.Secret));
                var sigBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(json));
                var sig = "sha256=" + Convert.ToHexString(sigBytes).ToLowerInvariant();
                req.Headers.TryAddWithoutValidation("X-Bestgen-Signature", sig);
            }

            using var res = await _http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            delivery.ResponseStatus = (int)res.StatusCode;
            delivery.ResponseBody = body.Length > 1900 ? body[..1900] : body;
            delivery.Attempts = 1;

            if (res.IsSuccessStatusCode)
            {
                delivery.Status = WebhookDeliveryStatus.Delivered;
                delivery.DeliveredAt = DateTime.UtcNow;
                var subscriber = await _db.Webhooks.FirstOrDefaultAsync(x => x.Id == w.Id, ct);
                if (subscriber is not null)
                {
                    subscriber.LastDeliveredAt = DateTime.UtcNow;
                    subscriber.FailureCount = 0;
                }
            }
            else
            {
                delivery.Status = WebhookDeliveryStatus.Failed;
                delivery.Error = $"HTTP {(int)res.StatusCode}";
                var subscriber = await _db.Webhooks.FirstOrDefaultAsync(x => x.Id == w.Id, ct);
                if (subscriber is not null) subscriber.FailureCount += 1;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Webhook {Id} delivery failed", w.Id);
            delivery.Status = WebhookDeliveryStatus.Failed;
            delivery.Error = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
            delivery.Attempts = 1;
        }

        _db.WebhookDeliveries.Add(delivery);
        await _db.SaveChangesAsync(ct);
    }
}

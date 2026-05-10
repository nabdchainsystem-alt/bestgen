using System.Collections.Concurrent;

namespace bestgen.Services.Observability;

/// <summary>
/// Lightweight per-IP rate limiter on POSTs to <c>/Identity/Account/Login</c>.
/// Sits separately from the global <c>AddRateLimiter</c> setup because the
/// Identity default-UI pages don't expose endpoint metadata where attribute
/// filters would normally bind.
///
/// 10 attempts per 5 minutes per IP. Hitting the limit returns 429 with
/// Retry-After. Resets when the window rolls.
/// </summary>
public class LoginRateLimitMiddleware
{
    private const int MaxAttempts = 10;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    // (count, windowStart). Evicted lazily when a key resets after window expiry.
    private static readonly ConcurrentDictionary<string, (int Count, DateTime WindowStart)> Hits = new();

    private readonly RequestDelegate _next;
    private readonly ILogger<LoginRateLimitMiddleware> _logger;

    public LoginRateLimitMiddleware(RequestDelegate next, ILogger<LoginRateLimitMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext ctx)
    {
        if (HttpMethods.IsPost(ctx.Request.Method)
            && ctx.Request.Path.StartsWithSegments("/Identity/Account/Login"))
        {
            var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var now = DateTime.UtcNow;

            var updated = Hits.AddOrUpdate(ip,
                _ => (1, now),
                (_, prev) => (now - prev.WindowStart > Window) ? (1, now) : (prev.Count + 1, prev.WindowStart));

            if (updated.Count > MaxAttempts)
            {
                _logger.LogWarning("Login rate limit hit by {Ip} ({Count} attempts in window)", ip, updated.Count);
                ctx.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                ctx.Response.Headers["Retry-After"] = ((int)Window.TotalSeconds).ToString();
                await ctx.Response.WriteAsync("Too many login attempts. Try again in a few minutes.");
                return;
            }
        }
        await _next(ctx);
    }
}

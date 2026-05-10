namespace bestgen.Services.Observability;

/// <summary>
/// Adds a baseline of defensive HTTP headers. The CSP is intentionally
/// permissive (allows inline styles + scripts) because the existing Razor
/// views ship inline CSS/JS — tightening to nonce-based CSP is a future pass.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) { _next = next; }

    public async Task Invoke(HttpContext ctx)
    {
        var h = ctx.Response.Headers;

        if (!h.ContainsKey("X-Content-Type-Options")) h["X-Content-Type-Options"] = "nosniff";
        if (!h.ContainsKey("X-Frame-Options")) h["X-Frame-Options"] = "SAMEORIGIN";
        if (!h.ContainsKey("Referrer-Policy")) h["Referrer-Policy"] = "strict-origin-when-cross-origin";
        if (!h.ContainsKey("Permissions-Policy")) h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

        // CSP: permissive enough for Bootstrap CDN, jsdelivr fonts, and inline event handlers
        // already in the Razor views. Tighten when those are migrated to external files.
        if (!h.ContainsKey("Content-Security-Policy"))
        {
            h["Content-Security-Policy"] =
                "default-src 'self'; " +
                "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.jsdelivr.net; " +
                "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://fonts.googleapis.com; " +
                "img-src 'self' data: https:; " +
                "font-src 'self' https://fonts.gstatic.com https://cdn.jsdelivr.net data:; " +
                "connect-src 'self'; " +
                "frame-ancestors 'self'; " +
                "base-uri 'self'; " +
                "form-action 'self'";
        }

        await _next(ctx);
    }
}

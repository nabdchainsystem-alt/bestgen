using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using bestgen.Data;
using bestgen.Services.Tenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace bestgen.Services.Api;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions { }

/// <summary>
/// Reads <c>X-Api-Key</c> from the incoming request, hashes it, and looks up
/// an active <see cref="bestgen.Models.ApiKey"/>. On match, signs the principal
/// in with a synthetic identity that carries the tenant id so the existing
/// query-filter pipeline scopes everything correctly.
/// </summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : base(options, logger, encoder) { }

    public static string Hash(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string GenerateRawKey()
    {
        // 32 bytes → 64 hex chars; prefix so customers recognize Bestgen keys.
        var rnd = new byte[32];
        RandomNumberGenerator.Fill(rnd);
        return $"bgk_{Convert.ToHexString(rnd).ToLowerInvariant()}";
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return AuthenticateResult.NoResult();
        }

        var db = Context.RequestServices.GetRequiredService<ApplicationDbContext>();
        var hash = Hash(raw.ToString().Trim());

        // Bypass tenant filter — API keys live in the db with their own TenantId.
        var key = await db.ApiKeys.IgnoreQueryFilters().FirstOrDefaultAsync(k => k.KeyHash == hash && k.IsActive);
        if (key is null)
        {
            return AuthenticateResult.Fail("Invalid API key.");
        }

        // The key carries a TenantId via the shadow column.
        var tenantId = db.Entry(key).Property<int>("TenantId").CurrentValue;

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, $"apikey:{key.Name}"),
            new(ClaimTypes.NameIdentifier, $"apikey-{key.Id}"),
            new("tenant_id", tenantId.ToString()),
            new("scopes", key.Scopes ?? "*"),
            new("auth_via", "api_key")
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);

        // Update last-used timestamp asynchronously (don't block the request).
        _ = Task.Run(async () =>
        {
            try
            {
                key.LastUsedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
            catch { /* best effort */ }
        });

        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }
}

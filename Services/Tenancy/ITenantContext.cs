using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace bestgen.Services.Tenancy;

/// <summary>
/// Resolves the current tenant id for the request. Reads the "tenant_id"
/// claim from the authenticated principal (added by
/// <see cref="AppUserClaimsPrincipalFactory"/>); falls back to the default
/// tenant for unauthenticated requests so seed/migration paths still work.
/// </summary>
public interface ITenantContext
{
    int TenantId { get; }
    bool IsAuthenticated { get; }
}

public class TenantContext : ITenantContext
{
    public const string ClaimType = "tenant_id";
    public const int DefaultTenantId = 1;

    private readonly IHttpContextAccessor _accessor;
    private int? _cached;

    public TenantContext(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public int TenantId
    {
        get
        {
            if (_cached.HasValue) return _cached.Value;

            var principal = _accessor.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated == true)
            {
                var claim = principal.FindFirst(ClaimType);
                if (claim is not null && int.TryParse(claim.Value, out var id) && id > 0)
                {
                    _cached = id;
                    return id;
                }
            }
            _cached = DefaultTenantId;
            return DefaultTenantId;
        }
    }

    public bool IsAuthenticated =>
        _accessor.HttpContext?.User?.Identity?.IsAuthenticated == true;
}

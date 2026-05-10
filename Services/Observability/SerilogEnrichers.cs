using System.Security.Claims;
using bestgen.Services.Tenancy;
using Microsoft.AspNetCore.Http;
using Serilog.Core;
using Serilog.Events;

namespace bestgen.Services.Observability;

/// <summary>
/// Adds TenantId, UserId, UserName, and RequestId properties to every log
/// event when a request scope is in flight. Configured in Program.cs as part
/// of the Serilog pipeline.
/// </summary>
public class HttpContextEnricher : ILogEventEnricher
{
    private readonly IHttpContextAccessor _http;

    public HttpContextEnricher(IHttpContextAccessor http) { _http = http; }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory pf)
    {
        var ctx = _http.HttpContext;
        if (ctx is null) return;

        var user = ctx.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            var name = user.Identity.Name;
            if (!string.IsNullOrEmpty(name))
            {
                logEvent.AddPropertyIfAbsent(pf.CreateProperty("UserName", name));
            }
            var uid = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(uid))
            {
                logEvent.AddPropertyIfAbsent(pf.CreateProperty("UserId", uid));
            }

            var tenantClaim = user.FindFirstValue("tenant_id");
            if (!string.IsNullOrEmpty(tenantClaim))
            {
                logEvent.AddPropertyIfAbsent(pf.CreateProperty("TenantId", tenantClaim));
            }
        }

        var rid = ctx.TraceIdentifier;
        if (!string.IsNullOrEmpty(rid))
        {
            logEvent.AddPropertyIfAbsent(pf.CreateProperty("RequestId", rid));
        }
    }
}

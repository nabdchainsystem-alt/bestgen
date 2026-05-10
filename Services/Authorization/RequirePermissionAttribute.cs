using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace bestgen.Services.Authorization;

/// <summary>
/// Action / controller filter that requires the current user to hold a
/// granular permission code (e.g. <c>invoices.delete</c>). Owners bypass.
/// Unauthenticated → 401. Authenticated but missing → 403.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class RequirePermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    public string Code { get; }
    public RequirePermissionAttribute(string code) { Code = code; }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            context.Result = new ChallengeResult();
            return;
        }
        if (user.IsInRole("Owner")) return;

        var svc = context.HttpContext.RequestServices.GetRequiredService<PermissionService>();
        if (!await svc.HasPermissionAsync(user, Code))
        {
            context.Result = new ForbidResult();
        }
    }
}

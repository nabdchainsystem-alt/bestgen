using Hangfire.Dashboard;

namespace bestgen.Services;

/// <summary>
/// Restricts the Hangfire dashboard at <c>/hangfire</c> to authenticated users
/// with the Owner or Admin role. Without this anyone could enqueue/delete jobs.
/// </summary>
public class HangfireAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();
        var user = http.User;
        if (user?.Identity?.IsAuthenticated != true) return false;
        return user.IsInRole("Owner") || user.IsInRole("Admin");
    }
}

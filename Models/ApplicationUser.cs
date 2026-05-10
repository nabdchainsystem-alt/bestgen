using Microsoft.AspNetCore.Identity;

namespace bestgen.Models;

public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }
    public string PreferredLanguage { get; set; } = "ar-SA";

    /// <summary>
    /// The tenant this user is currently working in. Surfaced as a "tenant_id"
    /// claim on the auth cookie via AppUserClaimsPrincipalFactory, then read
    /// by ITenantContext to scope every query/save.
    /// </summary>
    public int CurrentTenantId { get; set; } = 1;

    /// <summary>
    /// When set, this user is a portal-only customer (role = "Customer") tied
    /// to a specific Customer record. Workspace routes are blocked; the user
    /// only sees their own invoices/quotations at /portal.
    /// </summary>
    public int? CustomerId { get; set; }
}

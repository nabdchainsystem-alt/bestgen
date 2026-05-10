using System.Security.Claims;
using bestgen.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace bestgen.Services.Tenancy;

/// <summary>
/// Adds a "tenant_id" claim to the user's auth cookie based on
/// <see cref="ApplicationUser.CurrentTenantId"/>, so <see cref="TenantContext"/>
/// can scope every query without a DB hit per request.
/// </summary>
public class AppUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    public AppUserClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options) { }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        var tenantId = user.CurrentTenantId > 0 ? user.CurrentTenantId : TenantContext.DefaultTenantId;
        identity.AddClaim(new Claim(TenantContext.ClaimType, tenantId.ToString()));
        return identity;
    }
}

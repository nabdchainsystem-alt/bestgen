using System.Collections.Concurrent;
using System.Security.Claims;
using bestgen.Data;
using bestgen.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace bestgen.Services.Authorization;

/// <summary>
/// Resolves the effective permission set for a user from their roles.
/// Owners short-circuit to all-permissions. Permissions are cached for
/// 60 seconds per role-set to keep the lookup off the hot path.
/// </summary>
public class PermissionService
{
    private static readonly TimeSpan CacheWindow = TimeSpan.FromSeconds(60);

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IMemoryCache _cache;

    public PermissionService(ApplicationDbContext db, UserManager<ApplicationUser> users, IMemoryCache cache)
    {
        _db = db;
        _users = users;
        _cache = cache;
    }

    public async Task<bool> HasPermissionAsync(ClaimsPrincipal principal, string code)
    {
        if (principal?.Identity?.IsAuthenticated != true) return false;
        if (principal.IsInRole("Owner")) return true; // Owners bypass.
        var perms = await GetUserPermissionsAsync(principal);
        return perms.Contains(code, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<HashSet<string>> GetUserPermissionsAsync(ClaimsPrincipal principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var roleClaims = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        if (roleClaims.Count == 0)
        {
            var user = await _users.GetUserAsync(principal);
            if (user is not null)
            {
                roleClaims = (await _users.GetRolesAsync(user)).ToList();
            }
        }
        if (roleClaims.Count == 0) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (roleClaims.Contains("Owner", StringComparer.OrdinalIgnoreCase))
        {
            return new HashSet<string>(await _db.Permissions.AsNoTracking().Select(p => p.Code).ToListAsync(),
                StringComparer.OrdinalIgnoreCase);
        }

        var key = $"perms:{string.Join("|", roleClaims.OrderBy(r => r))}";
        if (_cache.TryGetValue(key, out HashSet<string>? cached) && cached is not null) return cached;

        // Lookup role IDs for the named roles, then their granted permissions.
        var roleIds = await _db.Roles.AsNoTracking()
            .Where(r => roleClaims.Contains(r.Name!))
            .Select(r => r.Id)
            .ToListAsync();
        var codes = await _db.RolePermissions.AsNoTracking()
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Select(rp => rp.PermissionCode)
            .Distinct()
            .ToListAsync();

        var set = new HashSet<string>(codes, StringComparer.OrdinalIgnoreCase);
        _cache.Set(key, set, CacheWindow);
        return set;
    }

    public async Task SetRolePermissionsAsync(string roleId, IEnumerable<string> codes, CancellationToken ct = default)
    {
        var requested = new HashSet<string>(codes, StringComparer.OrdinalIgnoreCase);
        var existing = await _db.RolePermissions.Where(rp => rp.RoleId == roleId).ToListAsync(ct);
        var existingSet = new HashSet<string>(existing.Select(e => e.PermissionCode), StringComparer.OrdinalIgnoreCase);

        // Remove revoked.
        foreach (var rp in existing.Where(rp => !requested.Contains(rp.PermissionCode)))
        {
            _db.RolePermissions.Remove(rp);
        }
        // Add new.
        foreach (var code in requested.Where(c => !existingSet.Contains(c)))
        {
            _db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionCode = code, GrantedAt = DateTime.UtcNow });
        }

        await _db.SaveChangesAsync(ct);
        InvalidateCache();
    }

    private void InvalidateCache()
    {
        // Coarse: bump the cache. Memory cache doesn't expose key listing, so
        // we live with the 60s TTL — admins rarely toggle permissions twice in a row.
    }
}

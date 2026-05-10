using bestgen.Data;
using bestgen.Models;
using bestgen.Services.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Controllers;

[Authorize(Roles = "Owner,Admin")]
[RequirePermission("permissions.manage")]
public class PermissionsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly RoleManager<IdentityRole> _roles;
    private readonly PermissionService _service;

    public PermissionsController(ApplicationDbContext db, RoleManager<IdentityRole> roles, PermissionService service)
    {
        _db = db;
        _roles = roles;
        _service = service;
    }

    public async Task<IActionResult> Index()
    {
        var permissions = await _db.Permissions.AsNoTracking()
            .OrderBy(p => p.Module).ThenBy(p => p.Code)
            .ToListAsync();
        // Owner gets a separate visual treatment — implicitly all-perms.
        var roles = await _roles.Roles.AsNoTracking()
            .Where(r => r.Name != "Owner" && r.Name != "Customer")
            .OrderBy(r => r.Name).ToListAsync();
        var grants = (await _db.RolePermissions.AsNoTracking().ToListAsync())
            .GroupBy(rp => rp.RoleId)
            .ToDictionary(g => g.Key, g => new HashSet<string>(g.Select(x => x.PermissionCode), StringComparer.OrdinalIgnoreCase));

        ViewBag.Roles = roles;
        ViewBag.Grants = grants;
        return View(permissions);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(string roleId, List<string>? codes)
    {
        codes ??= new List<string>();
        await _service.SetRolePermissionsAsync(roleId, codes);
        TempData["PermMessage"] = "Permissions saved.";
        return RedirectToAction(nameof(Index));
    }
}

using bestgen.Data;
using bestgen.Models;
using bestgen.Services.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Controllers;

[Authorize(Roles = "Owner,Admin")]
public class ApiKeysController : Controller
{
    private readonly ApplicationDbContext _db;
    public ApiKeysController(ApplicationDbContext db) { _db = db; }

    public async Task<IActionResult> Index()
    {
        var keys = await _db.ApiKeys.AsNoTracking().OrderByDescending(k => k.CreatedAt).ToListAsync();
        return View(keys);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [bestgen.Services.Authorization.RequirePermission("apikeys.manage")]
    public async Task<IActionResult> Create(string name, string? scopes)
    {
        if (string.IsNullOrWhiteSpace(name)) name = "Untitled key";
        var raw = ApiKeyAuthenticationHandler.GenerateRawKey();
        var entity = new ApiKey
        {
            Name = name.Trim(),
            KeyHash = ApiKeyAuthenticationHandler.Hash(raw),
            LastFour = raw[^4..],
            Scopes = string.IsNullOrWhiteSpace(scopes) ? "*" : scopes,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        };
        _db.ApiKeys.Add(entity);
        await _db.SaveChangesAsync();

        // Show the raw key once — caller must copy now.
        TempData["NewApiKey"] = raw;
        TempData["NewApiKeyName"] = entity.Name;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [bestgen.Services.Authorization.RequirePermission("apikeys.manage")]
    public async Task<IActionResult> Revoke(int id)
    {
        var k = await _db.ApiKeys.FirstOrDefaultAsync(x => x.Id == id);
        if (k is not null)
        {
            k.IsActive = false;
            k.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}

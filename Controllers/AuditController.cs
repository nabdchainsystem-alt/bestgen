using bestgen.Data;
using bestgen.Models;
using bestgen.Services.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Controllers;

[Authorize]
public class AuditController : Controller
{
    private const int PageSize = 50;

    private readonly ApplicationDbContext _db;
    private readonly AuditSink _sink;

    public AuditController(ApplicationDbContext db, AuditSink sink) { _db = db; _sink = sink; }

    [Authorize(Roles = "Owner,Admin")]
    public IActionResult Files()
    {
        ViewBag.SinkEnabled = _sink.Enabled;
        ViewBag.Directory = _sink.Directory;
        return View(_sink.ListFiles());
    }

    [Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> Download(string name)
    {
        try
        {
            var bytes = await _sink.ReadFileAsync(name);
            return File(bytes, "application/x-ndjson", name);
        }
        catch (FileNotFoundException) { return NotFound(); }
    }

    [Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> Verify(string name)
    {
        var result = await _sink.VerifyAsync(name);
        TempData["AuditVerifyResult"] = result.Ok
            ? $"✅ {name} — chain intact across {result.Lines} lines."
            : $"❌ {name} — {result.Message}";
        return RedirectToAction(nameof(Files));
    }

    public async Task<IActionResult> Index(
        string? q,
        string? entity,
        string? user,
        AuditAction? action,
        DateTime? from,
        DateTime? to,
        int page = 1)
    {
        if (page < 1) page = 1;

        var query = _db.AuditEntries.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(a =>
                (a.Summary != null && EF.Functions.Like(a.Summary, $"%{term}%"))
                || (a.EntityKey != null && EF.Functions.Like(a.EntityKey, $"%{term}%"))
                || EF.Functions.Like(a.EntityName, $"%{term}%"));
        }
        if (!string.IsNullOrWhiteSpace(entity))
        {
            query = query.Where(a => a.EntityName == entity);
        }
        if (!string.IsNullOrWhiteSpace(user))
        {
            query = query.Where(a => a.UserName == user);
        }
        if (action is not null)
        {
            query = query.Where(a => a.Action == action.Value);
        }
        if (from is not null)
        {
            var f = from.Value.Date;
            query = query.Where(a => a.At >= f);
        }
        if (to is not null)
        {
            var t = to.Value.Date.AddDays(1);
            query = query.Where(a => a.At < t);
        }

        var total = await query.CountAsync();
        var rows = await query
            .OrderByDescending(a => a.At)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        var entityNames = await _db.AuditEntries.AsNoTracking()
            .Select(a => a.EntityName).Distinct().OrderBy(n => n).Take(200).ToListAsync();
        var userNames = await _db.AuditEntries.AsNoTracking()
            .Where(a => a.UserName != null)
            .Select(a => a.UserName!).Distinct().OrderBy(n => n).Take(200).ToListAsync();

        ViewBag.Total = total;
        ViewBag.Page = page;
        ViewBag.PageSize = PageSize;
        ViewBag.PageCount = (int)Math.Ceiling(total / (double)PageSize);
        ViewBag.EntityNames = entityNames;
        ViewBag.UserNames = userNames;
        ViewBag.Filters = new { q, entity, user, action, from, to };
        return View(rows);
    }
}

using bestgen.Data;
using bestgen.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Controllers;

[Authorize]
public class BankReconciliationController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly BankReconciliationService _service;

    public BankReconciliationController(ApplicationDbContext db, BankReconciliationService service)
    {
        _db = db;
        _service = service;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.Statements = await _service.ListStatementsAsync();
        ViewBag.BankAccounts = await _db.BankAccounts.AsNoTracking().OrderBy(a => a.AccountName).ToListAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(15_000_000)]
    public async Task<IActionResult> Import(int bankAccountId, IFormFile file)
    {
        var (ok, err, stmt) = await _service.ImportCsvAsync(bankAccountId, file);
        if (!ok)
        {
            TempData["BankError"] = err;
            return RedirectToAction(nameof(Index));
        }
        TempData["BankMessage"] = $"Imported {stmt!.LineCount} line(s).";
        return RedirectToAction(nameof(Detail), new { id = stmt.Id });
    }

    public async Task<IActionResult> Detail(int id)
    {
        var stmt = await _service.GetStatementAsync(id);
        if (stmt is null) return NotFound();
        return View(stmt);
    }

    [HttpGet]
    public async Task<IActionResult> Suggest(int lineId)
    {
        var matches = await _service.SuggestMatchesAsync(lineId);
        return Json(matches.Select(j => new
        {
            id = j.Id,
            number = j.EntryNumber,
            date = j.EntryDate.ToString("yyyy-MM-dd"),
            description = j.Description,
            total = j.TotalDebit
        }));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Match(int lineId, int? journalEntryId, string? note, int statementId)
    {
        var name = User.Identity?.Name;
        await _service.MatchLineAsync(lineId, journalEntryId, name, note);
        TempData["BankMessage"] = "Line matched.";
        return RedirectToAction(nameof(Detail), new { id = statementId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unmatch(int lineId, int statementId)
    {
        await _service.UnmatchLineAsync(lineId);
        TempData["BankMessage"] = "Match removed.";
        return RedirectToAction(nameof(Detail), new { id = statementId });
    }
}

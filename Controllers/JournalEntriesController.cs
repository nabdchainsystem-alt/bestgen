using bestgen.Data;
using bestgen.Helpers;
using bestgen.Models;
using bestgen.Services;
using bestgen.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Controllers;

[Authorize(Roles = "Owner,Admin,Accountant")]
public class JournalEntriesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly JournalEntryService _service;

    public JournalEntriesController(ApplicationDbContext context, JournalEntryService service)
    {
        _context = context;
        _service = service;
    }

    public async Task<IActionResult> Index(string? q, string? status)
    {
        ViewBag.CurrentSearch = q;
        ViewBag.CurrentStatus = status;
        ViewBag.StatusOptions = Enum.GetValues<JournalEntryStatus>()
            .Select(value => new SelectListItem { Text = EntityDisplayHelper.TranslateEnum(value), Value = value.ToString() })
            .ToList();

        var query = _context.JournalEntries.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(entry =>
                entry.EntryNumber.Contains(q)
                || (entry.SourceModule != null && entry.SourceModule.Contains(q))
                || (entry.Description != null && entry.Description.Contains(q)));
        }

        if (Enum.TryParse<JournalEntryStatus>(status, out var entryStatus))
        {
            query = query.Where(entry => entry.Status == entryStatus);
        }

        return View(await query.OrderByDescending(entry => entry.EntryDate).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var entry = await _context.JournalEntries
            .AsNoTracking()
            .Include(x => x.Lines)
            .ThenInclude(line => line.Account)
            .FirstOrDefaultAsync(x => x.Id == id);

        return entry is null ? NotFound() : View(entry);
    }

    public async Task<IActionResult> Create()
    {
        await PopulateAccountsAsync();
        return View(new JournalEntryFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(JournalEntryFormViewModel model)
    {
        var lines = _service.BuildLines(model);
        foreach (var error in _service.Validate(model, lines))
        {
            ModelState.AddModelError("Lines", error);
        }

        if (!ModelState.IsValid)
        {
            await PopulateAccountsAsync();
            return View(model);
        }

        var entry = await _service.CreateAsync(model, lines);
        return RedirectToAction(nameof(Details), new { id = entry.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var entry = await _context.JournalEntries
            .AsNoTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (entry is null)
        {
            return NotFound();
        }

        await PopulateAccountsAsync();
        return View(_service.ToFormViewModel(entry));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, JournalEntryFormViewModel model)
    {
        var lines = _service.BuildLines(model);
        foreach (var error in _service.Validate(model, lines))
        {
            ModelState.AddModelError("Lines", error);
        }

        if (!ModelState.IsValid)
        {
            await PopulateAccountsAsync();
            return View(model);
        }

        await _service.UpdateAsync(id, model, lines);
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Delete(int id)
    {
        var entry = await _context.JournalEntries.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return entry is null ? NotFound() : View(entry);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var entry = await _context.JournalEntries.FindAsync(id);
        if (entry is not null)
        {
            _context.JournalEntries.Remove(entry);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateAccountsAsync()
    {
        ViewBag.Accounts = await _context.Accounts.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.AccountCode).ToListAsync();
        ViewBag.Statuses = Enum.GetValues<JournalEntryStatus>();
    }
}

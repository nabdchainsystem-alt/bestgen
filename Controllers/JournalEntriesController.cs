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
    private readonly AccountingService _accountingService;

    public JournalEntriesController(ApplicationDbContext context, AccountingService accountingService)
    {
        _context = context;
        _accountingService = accountingService;
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

        if (entry is null)
        {
            return NotFound();
        }

        return View(entry);
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
        var lines = BuildLines(model);
        ValidateJournal(model, lines);

        if (!ModelState.IsValid)
        {
            await PopulateAccountsAsync();
            return View(model);
        }

        var entry = new JournalEntry
        {
            EntryNumber = string.IsNullOrWhiteSpace(model.EntryNumber) ? await GenerateEntryNumberAsync() : model.EntryNumber,
            EntryDate = model.EntryDate,
            SourceModule = model.SourceModule,
            Description = model.Description,
            Status = model.Status,
            TotalDebit = lines.Sum(line => line.Debit),
            TotalCredit = lines.Sum(line => line.Credit),
            Lines = lines
        };

        _context.JournalEntries.Add(entry);
        await _context.SaveChangesAsync();
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
        return View(ToFormViewModel(entry));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, JournalEntryFormViewModel model)
    {
        var entry = await _context.JournalEntries
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (entry is null)
        {
            return NotFound();
        }

        var lines = BuildLines(model);
        ValidateJournal(model, lines);

        if (!ModelState.IsValid)
        {
            await PopulateAccountsAsync();
            return View(model);
        }

        entry.EntryDate = model.EntryDate;
        entry.SourceModule = model.SourceModule;
        entry.Description = model.Description;
        entry.Status = model.Status;
        entry.TotalDebit = lines.Sum(line => line.Debit);
        entry.TotalCredit = lines.Sum(line => line.Credit);
        _context.JournalEntryLines.RemoveRange(entry.Lines);
        foreach (var line in lines)
        {
            line.JournalEntryId = entry.Id;
        }
        await _context.JournalEntryLines.AddRangeAsync(lines);

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Delete(int id)
    {
        var entry = await _context.JournalEntries.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (entry is null)
        {
            return NotFound();
        }

        return View(entry);
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

    private List<JournalEntryLine> BuildLines(JournalEntryFormViewModel model)
    {
        var lines = model.Lines
            .Where(line => line.AccountId > 0 && (line.Debit > 0 || line.Credit > 0))
            .Select(line => new JournalEntryLine
            {
                AccountId = line.AccountId,
                Debit = line.Debit,
                Credit = line.Credit,
                Description = line.Description
            })
            .ToList();

        model.TotalDebit = lines.Sum(line => line.Debit);
        model.TotalCredit = lines.Sum(line => line.Credit);
        return lines;
    }

    private void ValidateJournal(JournalEntryFormViewModel model, List<JournalEntryLine> lines)
    {
        if (lines.Count < 2)
        {
            ModelState.AddModelError("Lines", "يجب إضافة سطرين على الأقل للقيد.");
        }

        if (model.Status == JournalEntryStatus.Posted && !_accountingService.IsBalanced(lines))
        {
            ModelState.AddModelError("Lines", "لا يمكن ترحيل القيد قبل تساوي إجمالي المدين والدائن.");
        }
    }

    private JournalEntryFormViewModel ToFormViewModel(JournalEntry entry) => new()
    {
        Id = entry.Id,
        EntryNumber = entry.EntryNumber,
        EntryDate = entry.EntryDate,
        SourceModule = entry.SourceModule,
        Description = entry.Description,
        Status = entry.Status,
        TotalDebit = entry.TotalDebit,
        TotalCredit = entry.TotalCredit,
        Lines = entry.Lines.Select(line => new JournalEntryLineFormViewModel
        {
            AccountId = line.AccountId,
            Debit = line.Debit,
            Credit = line.Credit,
            Description = line.Description
        }).ToList()
    };

    private async Task<string> GenerateEntryNumberAsync()
    {
        var next = await _context.JournalEntries.CountAsync() + 1;
        return $"JE-{next:00000}";
    }
}

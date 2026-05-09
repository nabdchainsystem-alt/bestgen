using bestgen.Data;
using bestgen.Models;
using bestgen.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services;

/// <summary>
/// Encapsulates the rules for hand-entered journal entries: line construction,
/// balance validation (only enforced when posting), number generation. Keeps
/// the controller thin and gives other features a single place to programmatically
/// create journals from arbitrary inputs.
/// </summary>
public class JournalEntryService
{
    private readonly ApplicationDbContext _context;
    private readonly AccountingService _accounting;

    public JournalEntryService(ApplicationDbContext context, AccountingService accounting)
    {
        _context = context;
        _accounting = accounting;
    }

    public List<JournalEntryLine> BuildLines(JournalEntryFormViewModel model)
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

    public IReadOnlyList<string> Validate(JournalEntryFormViewModel model, List<JournalEntryLine> lines)
    {
        var errors = new List<string>();
        if (lines.Count < 2)
        {
            errors.Add("A journal entry must have at least two lines.");
        }
        if (model.Status == JournalEntryStatus.Posted && !_accounting.IsBalanced(lines))
        {
            errors.Add("Cannot post a journal entry until total debits equal total credits.");
        }
        return errors;
    }

    public async Task<JournalEntry> CreateAsync(JournalEntryFormViewModel model, List<JournalEntryLine> lines)
    {
        var entry = new JournalEntry
        {
            EntryNumber = string.IsNullOrWhiteSpace(model.EntryNumber)
                ? await _accounting.GenerateEntryNumberAsync()
                : model.EntryNumber,
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
        return entry;
    }

    public async Task UpdateAsync(int id, JournalEntryFormViewModel model, List<JournalEntryLine> lines)
    {
        var entry = await _context.JournalEntries
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (entry is null)
        {
            throw new InvalidOperationException($"Journal entry {id} was not found.");
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
    }

    public JournalEntryFormViewModel ToFormViewModel(JournalEntry entry) => new()
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
}

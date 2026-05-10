using System.Globalization;
using bestgen.Data;
using bestgen.Models;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services;

/// <summary>
/// Imports CSV bank statements and supports manual line-by-line reconciliation
/// against journal entries. CSV format expected: header row + columns
/// containing Date, Description, Amount, optional Balance, optional Reference.
/// Column order is detected by header keywords; Arabic + English headers ok.
/// </summary>
public class BankReconciliationService
{
    private readonly ApplicationDbContext _db;

    public BankReconciliationService(ApplicationDbContext db) { _db = db; }

    public async Task<(bool Success, string? Error, BankStatement? Statement)> ImportCsvAsync(
        int bankAccountId, IFormFile file, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0) return (false, "Empty file.", null);

        using var reader = new StreamReader(file.OpenReadStream());
        var csv = await reader.ReadToEndAsync(ct);
        return await ImportCsvAsync(bankAccountId, file.FileName, csv, ct);
    }

    public async Task<(bool Success, string? Error, BankStatement? Statement)> ImportCsvAsync(
        int bankAccountId, string fileName, string csv, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(csv)) return (false, "Empty CSV.", null);
        if (!await _db.BankAccounts.AnyAsync(a => a.Id == bankAccountId, ct))
        {
            return (false, "Bank account not found.", null);
        }

        var lines = csv.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2) return (false, "CSV needs a header row + at least one data row.", null);

        var headers = ParseCsvLine(lines[0]).Select(h => h.Trim().ToLowerInvariant()).ToList();
        var idxDate = FindHeaderIndex(headers, "date", "تاريخ");
        var idxDesc = FindHeaderIndex(headers, "description", "narrative", "details", "البيان", "الوصف");
        var idxAmount = FindHeaderIndex(headers, "amount", "value", "المبلغ");
        var idxDebit = FindHeaderIndex(headers, "debit", "withdrawal", "out", "مدين");
        var idxCredit = FindHeaderIndex(headers, "credit", "deposit", "in", "دائن");
        var idxBalance = FindHeaderIndex(headers, "balance", "running balance", "الرصيد");
        var idxRef = FindHeaderIndex(headers, "reference", "ref", "trx id", "transaction id", "المرجع");

        if (idxDate < 0 || (idxAmount < 0 && (idxDebit < 0 || idxCredit < 0)))
        {
            return (false, "CSV must have a Date column and either an Amount column or both Debit + Credit columns.", null);
        }

        var stmt = new BankStatement
        {
            BankAccountId = bankAccountId,
            FileName = fileName,
            ImportedAt = DateTime.UtcNow
        };

        DateTime? minDate = null, maxDate = null;
        decimal openingBalance = 0, closingBalance = 0;
        var first = true;

        for (var i = 1; i < lines.Length; i++)
        {
            var fields = ParseCsvLine(lines[i]);
            if (fields.Count == 0 || fields.All(string.IsNullOrWhiteSpace)) continue;

            string? raw(int idx) => idx >= 0 && idx < fields.Count ? fields[idx] : null;
            decimal parseDec(string? s) => string.IsNullOrWhiteSpace(s) ? 0m
                : decimal.TryParse(s.Replace(",", "").Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0m;

            if (!DateTime.TryParse(raw(idxDate), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date))
            {
                if (!DateTime.TryParse(raw(idxDate), out date))
                {
                    continue; // skip rows with unparseable dates
                }
            }

            decimal amount;
            if (idxAmount >= 0)
            {
                amount = parseDec(raw(idxAmount));
            }
            else
            {
                var deb = parseDec(raw(idxDebit));
                var cre = parseDec(raw(idxCredit));
                amount = cre - deb; // credit positive, debit negative
            }

            var bal = idxBalance >= 0 ? (decimal?)parseDec(raw(idxBalance)) : null;
            var refTxt = raw(idxRef);
            var desc = raw(idxDesc) ?? "";

            stmt.Lines.Add(new BankStatementLine
            {
                Date = date,
                Description = desc.Length > 400 ? desc.Substring(0, 400) : desc,
                Reference = refTxt?.Length > 80 ? refTxt.Substring(0, 80) : refTxt,
                Amount = amount,
                Balance = bal,
                IsMatched = false
            });

            if (first) { openingBalance = bal ?? 0m; first = false; }
            closingBalance = bal ?? closingBalance;
            minDate = minDate is null || date < minDate ? date : minDate;
            maxDate = maxDate is null || date > maxDate ? date : maxDate;
        }

        stmt.LineCount = stmt.Lines.Count;
        stmt.PeriodStart = minDate;
        stmt.PeriodEnd = maxDate;
        stmt.OpeningBalance = openingBalance;
        stmt.ClosingBalance = closingBalance;

        _db.BankStatements.Add(stmt);
        await _db.SaveChangesAsync(ct);
        return (true, null, stmt);
    }

    public Task<BankStatement?> GetStatementAsync(int id, CancellationToken ct = default)
    {
        return _db.BankStatements.AsNoTracking()
            .Include(s => s.BankAccount)
            .Include(s => s.Lines.OrderBy(l => l.Date))
            .ThenInclude(l => l.MatchedJournalEntry)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public Task<List<BankStatement>> ListStatementsAsync(CancellationToken ct = default)
    {
        return _db.BankStatements.AsNoTracking()
            .Include(s => s.BankAccount)
            .OrderByDescending(s => s.ImportedAt)
            .ToListAsync(ct);
    }

    public async Task<bool> MatchLineAsync(int lineId, int? journalEntryId, string? userName, string? note, CancellationToken ct = default)
    {
        var line = await _db.BankStatementLines.FirstOrDefaultAsync(l => l.Id == lineId, ct);
        if (line is null) return false;
        line.IsMatched = true;
        line.MatchedJournalEntryId = journalEntryId;
        line.MatchedAt = DateTime.UtcNow;
        line.MatchedByUserName = userName;
        if (!string.IsNullOrWhiteSpace(note)) line.Notes = note;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> UnmatchLineAsync(int lineId, CancellationToken ct = default)
    {
        var line = await _db.BankStatementLines.FirstOrDefaultAsync(l => l.Id == lineId, ct);
        if (line is null) return false;
        line.IsMatched = false;
        line.MatchedJournalEntryId = null;
        line.MatchedAt = null;
        line.MatchedByUserName = null;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<JournalEntry>> SuggestMatchesAsync(int lineId, CancellationToken ct = default)
    {
        var line = await _db.BankStatementLines.AsNoTracking().FirstOrDefaultAsync(l => l.Id == lineId, ct);
        if (line is null) return new List<JournalEntry>();
        var dayWindow = TimeSpan.FromDays(7);
        var lo = line.Date - dayWindow;
        var hi = line.Date + dayWindow;
        var amountAbs = Math.Abs(line.Amount);

        var candidates = await _db.JournalEntries
            .AsNoTracking()
            .Include(j => j.Lines)
            .Where(j => j.EntryDate >= lo && j.EntryDate <= hi
                     && j.Lines.Any(l => l.Debit == amountAbs || l.Credit == amountAbs))
            .Take(50)
            .ToListAsync(ct);

        // Order by closeness to the line date in memory (provider-agnostic).
        return candidates
            .OrderBy(j => Math.Abs((j.EntryDate - line.Date).TotalDays))
            .Take(10)
            .ToList();
    }

    // ---------- helpers ----------
    private static int FindHeaderIndex(List<string> headers, params string[] names)
    {
        foreach (var n in names)
        {
            var i = headers.IndexOf(n.ToLowerInvariant());
            if (i >= 0) return i;
        }
        // fuzzy contains
        for (var i = 0; i < headers.Count; i++)
        {
            foreach (var n in names)
            {
                if (headers[i].Contains(n.ToLowerInvariant())) return i;
            }
        }
        return -1;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(line)) return result;
        var sb = new System.Text.StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"'); i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(sb.ToString()); sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }
        result.Add(sb.ToString());
        return result;
    }
}

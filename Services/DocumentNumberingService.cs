using bestgen.Data;
using bestgen.Models;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services;

/// <summary>
/// Centralised number generator for transactional documents. Resolves the
/// configured <see cref="NumberingPolicy"/> for a given document type and
/// returns the next formatted number, incrementing the persisted sequence.
/// Format tokens supported: {prefix}, {yyyy}, {MM}, {0000} (zero-padded counter).
/// </summary>
public class DocumentNumberingService
{
    private readonly ApplicationDbContext _context;

    public DocumentNumberingService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<string> NextAsync(string documentType, string fallbackPrefix, DateTime? when = null)
    {
        var policy = await _context.NumberingPolicies
            .FirstOrDefaultAsync(x => x.DocumentType == documentType);
        var date = when ?? DateTime.Today;

        if (policy is null)
        {
            return $"{fallbackPrefix}-{date:yyyy}-{1:00000}";
        }

        if (policy.ResetAnnually && policy.LastResetYear != date.Year)
        {
            policy.CurrentSequence = 0;
            policy.LastResetYear = date.Year;
        }

        policy.CurrentSequence += 1;
        policy.UpdatedAt = DateTime.UtcNow;

        return Format(policy, date);
    }

    public async Task<string> PeekAsync(string documentType, string fallbackPrefix, DateTime? when = null)
    {
        var policy = await _context.NumberingPolicies
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.DocumentType == documentType);
        var date = when ?? DateTime.Today;

        if (policy is null)
        {
            return $"{fallbackPrefix}-{date:yyyy}-{1:00000}";
        }

        var preview = new NumberingPolicy
        {
            Prefix = policy.Prefix,
            Format = policy.Format,
            CurrentSequence = policy.ResetAnnually && policy.LastResetYear != date.Year ? 1 : policy.CurrentSequence + 1
        };
        return Format(preview, date);
    }

    private static string Format(NumberingPolicy policy, DateTime date)
    {
        var pattern = string.IsNullOrWhiteSpace(policy.Format) ? "{prefix}-{yyyy}-{0000}" : policy.Format;
        var counterToken = ExtractCounterToken(pattern);
        var width = counterToken.Length;
        var counter = policy.CurrentSequence.ToString(new string('0', Math.Max(width, 1)));

        return pattern
            .Replace("{prefix}", policy.Prefix)
            .Replace("{yyyy}", date.ToString("yyyy"))
            .Replace("{MM}", date.ToString("MM"))
            .Replace("{" + counterToken + "}", counter);
    }

    private static string ExtractCounterToken(string pattern)
    {
        var start = pattern.IndexOf("{0", StringComparison.Ordinal);
        if (start < 0) return "0000";
        var end = pattern.IndexOf('}', start);
        if (end < 0) return "0000";
        return pattern.Substring(start + 1, end - start - 1);
    }
}

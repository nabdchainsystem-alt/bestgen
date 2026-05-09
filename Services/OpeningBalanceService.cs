using bestgen.Data;
using bestgen.Models;

namespace bestgen.Services;

/// <summary>
/// Brings an account's starting balance onto the books. Each opening-balance row
/// posts a 2-line journal entry: one line on the chosen account at the supplied
/// debit/credit, balanced by Opening Balance Equity (3200) on the opposite side.
/// This works uniformly whether the row is a balance-sheet asset, liability, or
/// equity item.
/// </summary>
public class OpeningBalanceService
{
    private readonly AccountingService _accounting;
    private readonly ChartOfAccounts _chart;

    public OpeningBalanceService(AccountingService accounting, ChartOfAccounts chart)
    {
        _accounting = accounting;
        _chart = chart;
    }

    public async Task PostAsync(OpeningBalance opening)
    {
        var debit = opening.Debit;
        var credit = opening.Credit;
        if (debit == 0 && credit == 0)
        {
            return;
        }

        var equity = await _chart.ResolveAsync(AccountCodes.OpeningBalanceEquity);

        var lines = new List<JournalEntryLine>
        {
            new() { AccountId = opening.AccountId, Debit = debit, Credit = credit, Description = "Opening balance" }
        };

        // Balance the entry by mirroring against Opening Balance Equity.
        if (debit > 0)
        {
            lines.Add(new() { AccountId = equity.Id, Debit = 0, Credit = debit, Description = "Opening balance equity" });
        }
        else
        {
            lines.Add(new() { AccountId = equity.Id, Debit = credit, Credit = 0, Description = "Opening balance equity" });
        }

        await _accounting.BuildAndAddEntryAsync(
            entryDate: opening.OpeningDate,
            sourceModule: nameof(OpeningBalance),
            description: $"Opening balance for account {opening.AccountId}",
            lines: lines);
    }
}

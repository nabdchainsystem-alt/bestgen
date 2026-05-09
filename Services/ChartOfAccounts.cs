using bestgen.Data;
using bestgen.Models;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services;

/// <summary>
/// Single source of truth for the canonical account codes used by every posting
/// service. Use the constants on <see cref="AccountCodes"/> instead of bare strings,
/// and <see cref="ChartOfAccounts.ResolveAsync(string)"/> to fetch the row.
/// </summary>
public static class AccountCodes
{
    // Assets
    public const string Cash = "1000";
    public const string Bank = "1010";
    public const string AccountsReceivable = "1100";
    public const string AccountsReceivableEmployees = "1110";
    public const string Inventory = "1200";
    public const string Prepaid = "1300";
    public const string FixedAssets = "1500";
    public const string AccumulatedDepreciation = "1510";

    // Liabilities — codes here match what DbSeeder already seeds.
    public const string AccountsPayable = "2000";
    public const string VatOutput = "2100";
    public const string VatInput = "2110";
    public const string SalariesPayable = "2200";
    public const string LoansPayable = "2220";
    public const string GoodsReceivedNotInvoiced = "2230";
    public const string CustomerAdvances = "2300";

    // Equity
    public const string Equity = "3000";
    public const string RetainedEarnings = "3100";
    public const string OpeningBalanceEquity = "3200";

    // Revenue
    public const string Sales = "4000";
    public const string ServiceRevenue = "4100";
    public const string AssetRentalRevenue = "4200";
    public const string SalesReturns = "4900";

    // Expenses / costs
    public const string CostOfGoodsSold = "5000";
    public const string RentExpense = "5100";
    public const string SalaryExpense = "5200";
    public const string BonusExpense = "5210";
    public const string UtilitiesExpense = "5300";
    public const string MiscExpense = "5400";
    public const string DepreciationExpense = "5700";
    public const string InventoryVariance = "5800";
    public const string PurchaseReturns = "5900";
}

/// <summary>
/// Thrown when a posting flow needs a canonical account that is missing from the
/// chart. The user-facing handler should surface this as a clear "missing account"
/// error pointing at <see cref="AccountCode"/> rather than a generic null reference.
/// </summary>
public sealed class MissingAccountException : InvalidOperationException
{
    public MissingAccountException(string accountCode)
        : base($"Required account '{accountCode}' is not present in the chart of accounts. Seed it before posting.")
    {
        AccountCode = accountCode;
    }

    public string AccountCode { get; }
}

public class ChartOfAccounts
{
    private readonly ApplicationDbContext _context;
    private readonly Dictionary<string, Account> _cache = new();

    public ChartOfAccounts(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Returns the account row for the given canonical code, throwing
    /// <see cref="MissingAccountException"/> if it is absent.
    /// Cached per request scope.
    /// </summary>
    public async Task<Account> ResolveAsync(string code)
    {
        if (_cache.TryGetValue(code, out var cached))
        {
            return cached;
        }

        var account = await _context.Accounts.FirstOrDefaultAsync(a => a.AccountCode == code)
            ?? throw new MissingAccountException(code);

        _cache[code] = account;
        return account;
    }

    public async Task<Account?> TryResolveAsync(string code)
    {
        if (_cache.TryGetValue(code, out var cached))
        {
            return cached;
        }

        var account = await _context.Accounts.FirstOrDefaultAsync(a => a.AccountCode == code);
        if (account is not null)
        {
            _cache[code] = account;
        }
        return account;
    }
}

using bestgen.Data;
using bestgen.Models;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services;

/// <summary>
/// On creation, books the asset onto the balance sheet:
///   DR Fixed Assets (1500) - bring asset onto books at PurchaseCost.
///   CR Cash (1000) - assumes immediate cash purchase.
/// If the asset is later sold or written off, that should post a separate
/// disposal entry — out of scope here.
/// </summary>
public class FixedAssetService
{
    private readonly AccountingService _accounting;
    private readonly ChartOfAccounts _chart;

    public FixedAssetService(AccountingService accounting, ChartOfAccounts chart)
    {
        _accounting = accounting;
        _chart = chart;
    }

    public async Task PostAcquisitionAsync(FixedAsset asset)
    {
        if (asset.Status != FixedAssetStatus.Active || asset.PurchaseCost <= 0)
        {
            return;
        }

        var fixedAssets = await _chart.ResolveAsync(AccountCodes.FixedAssets);
        var cash = await _chart.ResolveAsync(AccountCodes.Cash);

        await _accounting.BuildAndAddEntryAsync(
            entryDate: asset.PurchaseDate,
            sourceModule: nameof(FixedAsset),
            description: $"Asset acquisition {asset.AssetCode}",
            lines: new[]
            {
                new JournalEntryLine { AccountId = fixedAssets.Id, Debit = asset.PurchaseCost, Credit = 0, Description = "Fixed asset" },
                new JournalEntryLine { AccountId = cash.Id, Debit = 0, Credit = asset.PurchaseCost, Description = "Cash paid" }
            });
    }
}

/// <summary>
/// Generates the rental document number and persists. Recurring rental revenue
/// posting (monthly DR AR / CR rental revenue) needs a scheduled job — out of scope
/// for the initial wiring. Use a manual journal entry until that runner exists.
/// </summary>
public class AssetRentalService
{
    private readonly ApplicationDbContext _context;

    public AssetRentalService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task PrepareAsync(AssetRental rental)
    {
        if (string.IsNullOrWhiteSpace(rental.RentalNumber))
        {
            var next = await _context.AssetRentals.CountAsync() + 1;
            rental.RentalNumber = $"AR-{DateTime.Today:yyyy}-{next:00000}";
        }
    }
}

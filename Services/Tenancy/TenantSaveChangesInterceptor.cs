using bestgen.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace bestgen.Services.Tenancy;

/// <summary>
/// Stamps the shadow <c>TenantId</c> property on every newly inserted row
/// with the current tenant id. Without this, EF would write the column's
/// default value (1) regardless of the active tenant.
/// </summary>
public class TenantSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ITenantContext _tenantContext;

    public TenantSaveChangesInterceptor(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? db)
    {
        if (db is not ApplicationDbContext app) return;
        var tenantId = _tenantContext.TenantId;

        foreach (var entry in app.ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Added) continue;
            if (!ApplicationDbContext.IsTenantScoped(entry.Metadata.ClrType)) continue;

            // Only set if not already explicitly assigned (e.g. seeder).
            var current = entry.Property("TenantId").CurrentValue;
            if (current is null or 0)
            {
                entry.Property("TenantId").CurrentValue = tenantId;
            }
        }
    }
}

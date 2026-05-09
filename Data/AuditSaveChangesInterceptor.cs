using bestgen.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace bestgen.Data;

/// <summary>
/// Captures Create/Update/Delete events on tracked entities and writes a row
/// to AuditEntries inside the same SaveChanges call. Skips Identity tables
/// (AspNet*) and the AuditEntries table itself to avoid recursion.
/// </summary>
public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor _http;

    private static readonly HashSet<string> IgnoredEntities = new(StringComparer.Ordinal)
    {
        nameof(AuditEntry),
        "IdentityUser", "IdentityRole", "IdentityUserRole", "IdentityUserClaim",
        "IdentityRoleClaim", "IdentityUserLogin", "IdentityUserToken",
        "ApplicationUser"
    };

    public AuditSaveChangesInterceptor(IHttpContextAccessor http)
    {
        _http = http;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        AppendAuditEntries(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AppendAuditEntries(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AppendAuditEntries(DbContext? context)
    {
        if (context is null) return;

        var userName = _http.HttpContext?.User?.Identity?.Name ?? "system";
        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Where(e => !IgnoredEntities.Contains(e.Entity.GetType().Name))
            .ToList();

        if (entries.Count == 0) return;

        var auditRows = new List<AuditEntry>(entries.Count);
        foreach (var entry in entries)
        {
            var entityName = entry.Entity.GetType().Name;
            var action = entry.State switch
            {
                EntityState.Added => AuditAction.Create,
                EntityState.Modified => AuditAction.Update,
                EntityState.Deleted => AuditAction.Delete,
                _ => AuditAction.Update
            };

            string? key = null;
            try
            {
                var keyProp = entry.Metadata.FindPrimaryKey()?.Properties.FirstOrDefault();
                if (keyProp is not null)
                {
                    key = entry.Property(keyProp.Name).CurrentValue?.ToString();
                }
            }
            catch
            {
                // best-effort — never let audit logging break SaveChanges
            }

            auditRows.Add(new AuditEntry
            {
                EntityName = entityName,
                EntityKey = key,
                Action = action,
                UserName = userName,
                Summary = BuildSummary(entry),
                At = DateTime.UtcNow
            });
        }

        context.Set<AuditEntry>().AddRange(auditRows);
    }

    private static string? BuildSummary(EntityEntry entry)
    {
        if (entry.State != EntityState.Modified) return null;
        var changed = entry.Properties
            .Where(p => p.IsModified && !p.Metadata.IsPrimaryKey())
            .Take(8)
            .Select(p => p.Metadata.Name)
            .ToList();
        return changed.Count == 0 ? null : string.Join(", ", changed);
    }
}

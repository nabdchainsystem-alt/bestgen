using bestgen.Models;
using bestgen.Services.Audit;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace bestgen.Data;

/// <summary>
/// Captures Create/Update/Delete events on tracked entities and writes a row
/// to AuditEntries inside the same SaveChanges call. Skips Identity tables
/// (AspNet*) and the AuditEntries table itself to avoid recursion.
///
/// After the DB save succeeds, the same rows are mirrored to the
/// append-only file sink (<see cref="AuditSink"/>) for tamper-evidence.
/// </summary>
public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private static readonly AsyncLocal<List<AuditEntry>?> _pendingRows = new();

    private readonly IHttpContextAccessor _http;
    private readonly AuditSink _sink;

    private static readonly HashSet<string> IgnoredEntities = new(StringComparer.Ordinal)
    {
        nameof(AuditEntry),
        "IdentityUser", "IdentityRole", "IdentityUserRole", "IdentityUserClaim",
        "IdentityRoleClaim", "IdentityUserLogin", "IdentityUserToken",
        "ApplicationUser"
    };

    public AuditSaveChangesInterceptor(IHttpContextAccessor http, AuditSink sink)
    {
        _http = http;
        _sink = sink;
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

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        var rows = _pendingRows.Value;
        _pendingRows.Value = null;
        if (rows is { Count: > 0 })
        {
            await _sink.WriteAsync(rows, cancellationToken);
        }
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        var rows = _pendingRows.Value;
        _pendingRows.Value = null;
        if (rows is { Count: > 0 })
        {
            // Sync caller — fire-and-forget the async sink write so we don't block.
            _ = _sink.WriteAsync(rows);
        }
        return base.SavedChanges(eventData, result);
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
        _pendingRows.Value = auditRows;
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

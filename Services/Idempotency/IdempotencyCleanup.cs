using bestgen.Data;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services.Idempotency;

/// <summary>Periodically trims expired idempotency rows so the table doesn't grow unbounded.</summary>
public class IdempotencyCleanup
{
    private readonly ApplicationDbContext _db;

    public IdempotencyCleanup(ApplicationDbContext db) { _db = db; }

    public async Task RunAsync(CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow;
        await _db.IdempotencyKeys
            .Where(k => k.ExpiresAt < cutoff)
            .ExecuteDeleteAsync(ct);
    }
}

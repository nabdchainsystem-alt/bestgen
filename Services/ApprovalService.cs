using bestgen.Data;
using bestgen.Models;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services;

/// <summary>
/// Lightweight single-step approval workflow.
/// Compute "requires approval" by looking up an active <see cref="ApprovalPolicy"/>
/// for the document type whose <c>MinAmount</c> &lt;= the document amount.
/// Submitting creates an <see cref="ApprovalRequest"/>; approve/reject resolves it.
/// </summary>
public class ApprovalService
{
    private readonly ApplicationDbContext _db;

    public ApprovalService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<bool> RequiresApprovalAsync(ApprovalDocumentType type, decimal amount)
    {
        return await _db.ApprovalPolicies
            .AsNoTracking()
            .Where(p => p.IsActive && p.DocumentType == type && p.MinAmount <= amount)
            .AnyAsync();
    }

    /// <summary>Ordered list of policies that match the given doc type + amount, by SequenceOrder ascending.</summary>
    public Task<List<ApprovalPolicy>> GetChainAsync(ApprovalDocumentType type, decimal amount, CancellationToken ct = default)
    {
        return _db.ApprovalPolicies
            .AsNoTracking()
            .Where(p => p.IsActive && p.DocumentType == type && p.MinAmount <= amount)
            .OrderBy(p => p.SequenceOrder)
            .ThenBy(p => p.Id)
            .ToListAsync(ct);
    }

    public async Task<ApprovalRequest?> GetActiveAsync(ApprovalDocumentType type, int docId)
    {
        return await _db.ApprovalRequests
            .AsNoTracking()
            .Where(r => r.DocumentType == type && r.DocumentId == docId && r.Status == ApprovalStatus.Pending)
            .OrderByDescending(r => r.RequestedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<ApprovalRequest?> GetLatestAsync(ApprovalDocumentType type, int docId)
    {
        return await _db.ApprovalRequests
            .AsNoTracking()
            .Where(r => r.DocumentType == type && r.DocumentId == docId)
            .OrderByDescending(r => r.RequestedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<ApprovalRequest> SubmitAsync(
        ApprovalDocumentType type, int docId, string docNumber, decimal amount,
        string? userId, string? userName, CancellationToken ct = default)
    {
        // If a pending request exists, return it instead of creating a duplicate.
        var existing = await _db.ApprovalRequests
            .Where(r => r.DocumentType == type && r.DocumentId == docId && r.Status == ApprovalStatus.Pending)
            .FirstOrDefaultAsync(ct);
        if (existing is not null) return existing;

        var chain = await GetChainAsync(type, amount, ct);
        var req = new ApprovalRequest
        {
            DocumentType = type,
            DocumentId = docId,
            DocumentNumber = docNumber,
            Amount = amount,
            Status = ApprovalStatus.Pending,
            RequestedByUserId = userId,
            RequestedByName = userName,
            RequestedAt = DateTime.UtcNow,
            CurrentStep = 1,
            TotalSteps = Math.Max(1, chain.Count)
        };
        _db.ApprovalRequests.Add(req);
        await _db.SaveChangesAsync(ct);
        return req;
    }

    /// <summary>
    /// True if the user can act on the request at its <c>CurrentStep</c> — the chain's
    /// step-N policy either has no required role, or its role is one the user holds.
    /// </summary>
    public async Task<bool> CanResolveAsync(ApprovalRequest req, IEnumerable<string> userRoles, CancellationToken ct = default)
    {
        var roleSet = new HashSet<string>(userRoles ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        var chain = await GetChainAsync(req.DocumentType, req.Amount, ct);
        if (chain.Count == 0) return true; // request opened with no policy — allow anyone.
        var step = Math.Clamp(req.CurrentStep, 1, chain.Count);
        var policy = chain[step - 1];
        return string.IsNullOrWhiteSpace(policy.RequiredRole) || roleSet.Contains(policy.RequiredRole!);
    }

    /// <summary>Returns the policy for the request's current step, or null if the request was opened without a policy.</summary>
    public async Task<ApprovalPolicy?> GetCurrentStepPolicyAsync(ApprovalRequest req, CancellationToken ct = default)
    {
        var chain = await GetChainAsync(req.DocumentType, req.Amount, ct);
        if (chain.Count == 0) return null;
        var step = Math.Clamp(req.CurrentStep, 1, chain.Count);
        return chain[step - 1];
    }

    public async Task<ApprovalRequest?> ResolveAsync(
        int id, ApprovalStatus newStatus, string? userId, string? userName, string? comment,
        CancellationToken ct = default)
    {
        if (newStatus is not (ApprovalStatus.Approved or ApprovalStatus.Rejected))
        {
            throw new ArgumentException("ResolveAsync only accepts Approved or Rejected.", nameof(newStatus));
        }

        var req = await _db.ApprovalRequests.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (req is null) return null;
        if (req.Status != ApprovalStatus.Pending) return req;

        var stepLine = $"Step {req.CurrentStep}/{req.TotalSteps}: {newStatus.ToString().ToUpperInvariant()} by {userName ?? "?"} at {DateTime.UtcNow:yyyy-MM-dd HH:mm}{(string.IsNullOrWhiteSpace(comment) ? "" : " — " + comment)}";
        req.StepHistory = string.IsNullOrEmpty(req.StepHistory) ? stepLine : (req.StepHistory + "\n" + stepLine);

        if (newStatus == ApprovalStatus.Rejected)
        {
            // Any rejection ends the chain immediately.
            req.Status = ApprovalStatus.Rejected;
            req.ResolvedByUserId = userId;
            req.ResolvedByName = userName;
            req.ResolvedAt = DateTime.UtcNow;
            req.Comment = comment;
        }
        else if (req.CurrentStep >= req.TotalSteps)
        {
            // Final step approved — request fully approved.
            req.Status = ApprovalStatus.Approved;
            req.ResolvedByUserId = userId;
            req.ResolvedByName = userName;
            req.ResolvedAt = DateTime.UtcNow;
            req.Comment = comment;
        }
        else
        {
            // Advance to next step; status stays Pending.
            req.CurrentStep += 1;
        }

        await _db.SaveChangesAsync(ct);
        return req;
    }

    public async Task<List<ApprovalRequest>> ListPendingAsync(int take = 100, CancellationToken ct = default)
    {
        return await _db.ApprovalRequests
            .AsNoTracking()
            .Where(r => r.Status == ApprovalStatus.Pending)
            .OrderByDescending(r => r.RequestedAt)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<List<ApprovalRequest>> ListRecentAsync(int take = 100, CancellationToken ct = default)
    {
        return await _db.ApprovalRequests
            .AsNoTracking()
            .OrderByDescending(r => r.RequestedAt)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<List<ApprovalPolicy>> ListPoliciesAsync(CancellationToken ct = default)
    {
        // SQLite can't ORDER BY decimal in SQL — pull then sort in memory.
        var rows = await _db.ApprovalPolicies.AsNoTracking().ToListAsync(ct);
        return rows
            .OrderBy(p => p.DocumentType)
            .ThenBy(p => p.SequenceOrder)
            .ThenBy(p => p.MinAmount)
            .ToList();
    }

    public async Task SavePolicyAsync(ApprovalPolicy input, CancellationToken ct = default)
    {
        if (input.Id == 0)
        {
            _db.ApprovalPolicies.Add(input);
        }
        else
        {
            var existing = await _db.ApprovalPolicies.FirstOrDefaultAsync(p => p.Id == input.Id, ct);
            if (existing is null) return;
            existing.DocumentType = input.DocumentType;
            existing.MinAmount = input.MinAmount;
            existing.IsActive = input.IsActive;
            existing.RequiredRole = input.RequiredRole;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeletePolicyAsync(int id, CancellationToken ct = default)
    {
        var p = await _db.ApprovalPolicies.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return;
        _db.ApprovalPolicies.Remove(p);
        await _db.SaveChangesAsync(ct);
    }
}

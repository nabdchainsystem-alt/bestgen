using bestgen.Data;
using bestgen.Models;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services.Consolidation;

/// <summary>
/// Read-only roll-up of metrics across every <see cref="Tenant"/> in an
/// <see cref="Organization"/>. Bypasses the tenant query filter via
/// <c>IgnoreQueryFilters()</c> and re-applies a manual <c>TenantId IN (…)</c>
/// scope so we never leak data from a tenant the caller doesn't belong to.
/// </summary>
public class ConsolidationService
{
    private readonly ApplicationDbContext _db;

    public ConsolidationService(ApplicationDbContext db) { _db = db; }

    public async Task<Org?> GetCurrentOrgAsync(int callerTenantId, CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.AsNoTracking()
            .IgnoreQueryFilters()
            .Include(t => t.Organization)
            .FirstOrDefaultAsync(t => t.Id == callerTenantId, ct);
        if (tenant?.OrganizationId is null || tenant.Organization is null) return null;

        var siblings = await _db.Tenants.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(t => t.OrganizationId == tenant.OrganizationId && t.IsActive)
            .OrderBy(t => t.Name)
            .Select(t => new TenantRow(t.Id, t.Name, t.Slug))
            .ToListAsync(ct);

        return new Org(tenant.Organization.Id, tenant.Organization.Name, siblings);
    }

    public async Task<ConsolidatedReport> RollupAsync(IReadOnlyCollection<int> tenantIds, DateTime from, DateTime to, CancellationToken ct = default)
    {
        if (tenantIds.Count == 0) return ConsolidatedReport.Empty;

        var perTenant = new List<ConsolidatedRow>();
        decimal totRevenue = 0, totVat = 0, totOutstanding = 0, totReceipts = 0, totPayments = 0, totExpenses = 0;
        int totInvoices = 0;

        foreach (var tid in tenantIds)
        {
            var revenue = await _db.SalesInvoices.AsNoTracking().IgnoreQueryFilters()
                .Where(i => EF.Property<int>(i, "TenantId") == tid)
                .Where(i => i.InvoiceDate >= from && i.InvoiceDate < to)
                .Where(i => i.Status != InvoiceStatus.Draft && i.Status != InvoiceStatus.Cancelled)
                .GroupBy(_ => 1)
                .Select(g => new { count = g.Count(), grand = g.Sum(x => x.GrandTotal), vat = g.Sum(x => x.VatTotal) })
                .FirstOrDefaultAsync(ct);

            var outstanding = await _db.SalesInvoices.AsNoTracking().IgnoreQueryFilters()
                .Where(i => EF.Property<int>(i, "TenantId") == tid)
                .SumAsync(i => (decimal?)i.RemainingAmount, ct) ?? 0m;

            var receipts = await _db.SalesReceipts.AsNoTracking().IgnoreQueryFilters()
                .Where(r => EF.Property<int>(r, "TenantId") == tid && r.Date >= from && r.Date < to)
                .SumAsync(r => (decimal?)r.Amount, ct) ?? 0m;

            var payments = await _db.SupplierPayments.AsNoTracking().IgnoreQueryFilters()
                .Where(p => EF.Property<int>(p, "TenantId") == tid && p.Date >= from && p.Date < to)
                .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

            var expenses = await _db.Expenses.AsNoTracking().IgnoreQueryFilters()
                .Where(e => EF.Property<int>(e, "TenantId") == tid && e.ExpenseDate >= from && e.ExpenseDate < to)
                .SumAsync(e => (decimal?)e.TotalAmount, ct) ?? 0m;

            var tenantName = await _db.Tenants.AsNoTracking().IgnoreQueryFilters()
                .Where(t => t.Id == tid)
                .Select(t => t.Name).FirstOrDefaultAsync(ct) ?? $"Tenant #{tid}";

            var row = new ConsolidatedRow(
                tid, tenantName,
                revenue?.count ?? 0,
                revenue?.grand ?? 0m,
                revenue?.vat ?? 0m,
                outstanding,
                receipts,
                payments + expenses,
                receipts - payments - expenses);
            perTenant.Add(row);

            totInvoices += row.InvoiceCount;
            totRevenue += row.Revenue;
            totVat += row.Vat;
            totOutstanding += row.Outstanding;
            totReceipts += row.CashIn;
            // CashOut already aggregated above.
        }

        return new ConsolidatedReport(
            from, to,
            perTenant,
            totInvoices, totRevenue, totVat, totOutstanding,
            perTenant.Sum(r => r.CashIn),
            perTenant.Sum(r => r.CashOut),
            perTenant.Sum(r => r.NetCash));
    }
}

public sealed record TenantRow(int Id, string Name, string Slug);
public sealed record Org(int Id, string Name, IReadOnlyList<TenantRow> Tenants);

public sealed record ConsolidatedRow(
    int TenantId, string TenantName,
    int InvoiceCount,
    decimal Revenue, decimal Vat,
    decimal Outstanding,
    decimal CashIn, decimal CashOut, decimal NetCash);

public sealed record ConsolidatedReport(
    DateTime From, DateTime To,
    IReadOnlyList<ConsolidatedRow> PerTenant,
    int TotalInvoices, decimal TotalRevenue, decimal TotalVat, decimal TotalOutstanding,
    decimal TotalCashIn, decimal TotalCashOut, decimal TotalNetCash)
{
    public static readonly ConsolidatedReport Empty =
        new(DateTime.UtcNow, DateTime.UtcNow, Array.Empty<ConsolidatedRow>(),
            0, 0m, 0m, 0m, 0m, 0m, 0m);
}

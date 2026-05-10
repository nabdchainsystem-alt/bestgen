using bestgen.Data;
using bestgen.Models;
using Microsoft.EntityFrameworkCore;

namespace bestgen.Services;

/// <summary>
/// Saudi-specific payroll calculations: GOSI (General Organization for Social
/// Insurance) contributions and EOSB (End-of-Service Benefit / مكافأة نهاية الخدمة).
///
/// Rates as of 2026 — adjust the constants if MoL/GOSI publish new ones.
/// </summary>
public class SaudiPayrollService
{
    // Saudi nationals: 21.75% total — Employer 11.75% (2% hazards + 9% pension + 0.75% SANED) + Employee 10% (9% pension + 1% SANED).
    public const decimal GosiSaudiEmployer = 0.1175m;
    public const decimal GosiSaudiEmployee = 0.10m;
    // Non-Saudi: only 2% occupational hazards, paid by employer.
    public const decimal GosiNonSaudiEmployer = 0.02m;
    public const decimal GosiNonSaudiEmployee = 0m;

    // GOSI cap on the contribution base (SAR 45,000/month for the Saudi pension scheme).
    public const decimal GosiContributionCap = 45_000m;

    private readonly ApplicationDbContext _db;

    public SaudiPayrollService(ApplicationDbContext db) { _db = db; }

    /// <summary>
    /// Returns (employerContribution, employeeContribution) for a single month
    /// based on the employee's contribution base — typically Basic + Housing,
    /// capped at <see cref="GosiContributionCap"/>.
    /// </summary>
    public static (decimal Employer, decimal Employee) CalculateGosi(decimal contributionBase, bool isSaudi)
    {
        if (contributionBase <= 0) return (0m, 0m);
        var basis = Math.Min(contributionBase, GosiContributionCap);
        if (isSaudi)
        {
            return (Round(basis * GosiSaudiEmployer), Round(basis * GosiSaudiEmployee));
        }
        return (Round(basis * GosiNonSaudiEmployer), Round(basis * GosiNonSaudiEmployee));
    }

    /// <summary>
    /// Saudi labor law EOSB:
    ///   • &lt; 5 years of service: half-month per year
    ///   • ≥ 5 years of service: full month per year (with the first 5 still at half-month is the conservative
    ///     reading; the common-practice reading is full month for the entire tenure once you cross 5 years —
    ///     this method uses the common-practice reading: full month for all years if total ≥ 5)
    ///   • Pro-rated by month for partial years.
    /// Resignation modifiers (one-third / two-thirds) are not applied here — pass <paramref name="resignationFactor"/>
    /// (1.0 = full benefit, 2/3 = 5–10y resignation, 1/3 = &lt;5y resignation, etc.) if needed.
    /// </summary>
    public static decimal CalculateEosb(DateTime hireDate, DateTime endDate, decimal monthlyBasicWage, decimal monthlyHousing = 0m, decimal resignationFactor = 1m)
    {
        if (endDate <= hireDate || monthlyBasicWage <= 0) return 0m;
        var totalMonths = ((endDate.Year - hireDate.Year) * 12) + (endDate.Month - hireDate.Month);
        if (endDate.Day < hireDate.Day) totalMonths -= 1;
        if (totalMonths <= 0) return 0m;

        var totalYears = totalMonths / 12m;
        var basis = monthlyBasicWage + monthlyHousing;

        decimal monthsAccrued;
        if (totalYears < 5m)
        {
            monthsAccrued = totalYears * 0.5m;
        }
        else
        {
            // Full-month-per-year for every year (common practice).
            monthsAccrued = totalYears * 1m;
        }
        return Round(basis * monthsAccrued * resignationFactor);
    }

    public async Task<EosbForEmployeeResult?> CalculateEosbForEmployeeAsync(int employeeId, DateTime endDate, decimal resignationFactor = 1m, CancellationToken ct = default)
    {
        var emp = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId, ct);
        if (emp is null) return null;
        var amount = CalculateEosb(emp.HireDate, endDate, emp.Salary, emp.HousingAllowance, resignationFactor);
        var name = string.IsNullOrWhiteSpace(emp.FullNameEn) ? emp.FullNameAr : emp.FullNameEn!;
        return new EosbForEmployeeResult(emp.Id, name, emp.HireDate, endDate, emp.Salary, emp.HousingAllowance, amount);
    }

    public async Task<List<GosiForEmployeeResult>> CalculateGosiForActiveAsync(CancellationToken ct = default)
    {
        var employees = await _db.Employees.AsNoTracking()
            .Where(e => e.Status == EmployeeStatus.Active)
            .OrderBy(e => e.FullNameAr)
            .ToListAsync(ct);
        var rows = new List<GosiForEmployeeResult>();
        foreach (var e in employees)
        {
            var basis = e.Salary + e.HousingAllowance;
            var (er, ee) = CalculateGosi(basis, e.IsSaudi);
            // Prefer English name where available so the LTR view doesn't force Arabic.
            var name = string.IsNullOrWhiteSpace(e.FullNameEn) ? e.FullNameAr : e.FullNameEn!;
            rows.Add(new GosiForEmployeeResult(e.Id, name, e.IsSaudi, basis, er, ee, er + ee));
        }
        return rows;
    }

    private static decimal Round(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}

public sealed record EosbForEmployeeResult(int EmployeeId, string EmployeeName, DateTime HireDate, DateTime EndDate, decimal Salary, decimal Housing, decimal Amount);
public sealed record GosiForEmployeeResult(int EmployeeId, string EmployeeName, bool IsSaudi, decimal ContributionBase, decimal Employer, decimal Employee, decimal Total);

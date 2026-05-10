using bestgen.Services;
using FluentAssertions;
using Xunit;

namespace Bestgen.Tests;

public class SaudiPayrollServiceTests
{
    [Theory]
    [InlineData(10_000, true,  1175,  1000)]   // Saudi: 11.75% er + 10% ee
    [InlineData(10_000, false, 200,   0)]      // non-Saudi: 2% er only
    [InlineData(50_000, true,  5287.50, 4500)] // capped at 45,000 base
    [InlineData(0,      true,  0,     0)]
    public void Gosi_calculations_match_published_rates(decimal contributionBase, bool isSaudi, decimal expectedEr, decimal expectedEe)
    {
        var (er, ee) = SaudiPayrollService.CalculateGosi(contributionBase, isSaudi);
        er.Should().Be(expectedEr);
        ee.Should().Be(expectedEe);
    }

    [Fact]
    public void Eosb_under_5_years_is_half_month_per_year()
    {
        var hire = new DateTime(2022, 1, 1);
        var end  = new DateTime(2026, 1, 1); // exactly 4 years
        var amount = SaudiPayrollService.CalculateEosb(hire, end, monthlyBasicWage: 5000m);

        // 4 years × 0.5 month × 5000 = 10000
        amount.Should().Be(10_000m);
    }

    [Fact]
    public void Eosb_at_or_above_5_years_is_full_month_per_year()
    {
        var hire = new DateTime(2018, 1, 1);
        var end  = new DateTime(2026, 1, 1); // exactly 8 years
        var amount = SaudiPayrollService.CalculateEosb(hire, end, monthlyBasicWage: 5000m);

        // 8 years × 1 month × 5000 = 40000
        amount.Should().Be(40_000m);
    }

    [Fact]
    public void Eosb_includes_housing_allowance()
    {
        var hire = new DateTime(2024, 1, 1);
        var end  = new DateTime(2026, 1, 1); // 2 years
        var amount = SaudiPayrollService.CalculateEosb(hire, end, monthlyBasicWage: 4000m, monthlyHousing: 1000m);

        // 2 × 0.5 × (4000 + 1000) = 5000
        amount.Should().Be(5_000m);
    }

    [Fact]
    public void Eosb_resignation_factor_applies()
    {
        var hire = new DateTime(2018, 1, 1);
        var end  = new DateTime(2026, 1, 1); // 8 years
        var two_thirds = SaudiPayrollService.CalculateEosb(hire, end, monthlyBasicWage: 6000m, resignationFactor: 2m / 3m);

        // 8 × 1 × 6000 × 2/3 = 32000
        two_thirds.Should().Be(32_000m);
    }

    [Fact]
    public void Eosb_zero_for_invalid_dates_or_zero_wage()
    {
        SaudiPayrollService.CalculateEosb(new(2026, 1, 1), new(2025, 1, 1), 5000m).Should().Be(0m);
        SaudiPayrollService.CalculateEosb(new(2024, 1, 1), new(2026, 1, 1), 0m).Should().Be(0m);
    }
}

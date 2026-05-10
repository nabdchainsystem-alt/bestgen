using bestgen.Models;
using bestgen.Services;
using FluentAssertions;
using Xunit;

namespace Bestgen.Tests;

public class RecurringInvoiceServiceTests
{
    [Theory]
    [InlineData(RecurrenceFrequency.Daily,     1,  0)]
    [InlineData(RecurrenceFrequency.Weekly,    7,  0)]
    public void Compute_next_run_for_day_based_frequencies(RecurrenceFrequency freq, int expectedDays, int expectedMonths)
    {
        var current = new DateTime(2026, 5, 10);
        var next = RecurringInvoiceService.ComputeNextRun(current, freq);
        next.Should().Be(current.AddDays(expectedDays).AddMonths(expectedMonths));
    }

    [Fact]
    public void Monthly_advances_one_calendar_month_preserving_day()
    {
        var current = new DateTime(2026, 1, 31);
        var next = RecurringInvoiceService.ComputeNextRun(current, RecurrenceFrequency.Monthly);
        // AddMonths handles short months: Jan 31 → Feb 28 (or 29 in leap year)
        next.Should().Be(new DateTime(2026, 2, 28));
    }

    [Fact]
    public void Quarterly_advances_three_months()
    {
        var current = new DateTime(2026, 1, 15);
        var next = RecurringInvoiceService.ComputeNextRun(current, RecurrenceFrequency.Quarterly);
        next.Should().Be(new DateTime(2026, 4, 15));
    }

    [Fact]
    public void Yearly_advances_one_year()
    {
        var current = new DateTime(2026, 5, 10);
        var next = RecurringInvoiceService.ComputeNextRun(current, RecurrenceFrequency.Yearly);
        next.Should().Be(new DateTime(2027, 5, 10));
    }
}

using PersonalFinance.Financing.Domain;
using Xunit;

namespace PersonalFinance.Financing.Tests;

public class AccrualBoundaryTests {
    [Fact]
    public void Purchase_on_the_cutoff_day_falls_into_the_closing_cycle() {
        var cycle = BillingCycleCalculator.ResolveCycle(new DateOnly(2026, 3, 15), cutoffDay: 15);
        Assert.Equal(new BillingCycle(2026, 3), cycle);
    }

    [Fact]
    public void Purchase_the_day_after_the_cutoff_falls_into_the_next_cycle() {
        var cycle = BillingCycleCalculator.ResolveCycle(new DateOnly(2026, 3, 16), cutoffDay: 15);
        Assert.Equal(new BillingCycle(2026, 4), cycle);
    }

    [Fact]
    public void Purchase_after_a_december_cutoff_rolls_into_the_next_january() {
        var cycle = BillingCycleCalculator.ResolveCycle(new DateOnly(2026, 12, 20), cutoffDay: 15);
        Assert.Equal(new BillingCycle(2027, 1), cycle);
    }

    [Fact]
    public void Cutoff_day_beyond_the_month_length_clamps_to_the_last_day() {
        var cycle = BillingCycleCalculator.ResolveCycle(new DateOnly(2026, 2, 28), cutoffDay: 30);
        Assert.Equal(new BillingCycle(2026, 2), cycle);
    }

    [Fact]
    public void AddMonths_walks_forward_across_a_year_boundary() {
        Assert.Equal(new BillingCycle(2027, 2), new BillingCycle(2026, 11).AddMonths(3));
        Assert.Equal(new BillingCycle(2027, 1), new BillingCycle(2026, 12).AddMonths(1));
        Assert.Equal(new BillingCycle(2026, 11), new BillingCycle(2026, 11).AddMonths(0));
    }

    [Fact]
    public void IsClosedAsOf_flips_the_day_after_the_cutoff() {
        var cycle = new BillingCycle(2026, 3);
        Assert.False(cycle.IsClosedAsOf(new DateOnly(2026, 3, 14), cutoffDay: 15));
        Assert.False(cycle.IsClosedAsOf(new DateOnly(2026, 3, 15), cutoffDay: 15));
        Assert.True(cycle.IsClosedAsOf(new DateOnly(2026, 3, 16), cutoffDay: 15));
    }

    [Fact]
    public void IsClosedAsOf_clamps_the_cutoff_to_february_length() {
        var cycle = new BillingCycle(2026, 2);
        Assert.False(cycle.IsClosedAsOf(new DateOnly(2026, 2, 28), cutoffDay: 30));
        Assert.True(cycle.IsClosedAsOf(new DateOnly(2026, 3, 1), cutoffDay: 30));
    }
}

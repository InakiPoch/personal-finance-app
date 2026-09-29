using PersonalFinance.Financing.Application.Commands;
using Xunit;

namespace PersonalFinance.Financing.Tests;

public sealed class CreditorPaymentWaterfallTests {
    [Fact]
    public void Allocate_fills_cuotas_in_order_leaving_the_last_one_partial() {
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
        var ordered = ids.Select(id => (id, 40_000L)).ToList();
        var allocation = CreditorPaymentWaterfall.Allocate(ordered, 175_000);
        Assert.Equal(5, allocation.Count);
        for(var i = 0; i < 4; i++) {
            Assert.Equal(ids[i], allocation[i].InstallmentId);
            Assert.Equal(40_000, allocation[i].AmountMinorUnits);
        }
        Assert.Equal(ids[4], allocation[4].InstallmentId);
        Assert.Equal(15_000, allocation[4].AmountMinorUnits);
    }

    [Fact]
    public void Allocate_on_an_exact_multiple_fills_whole_cuotas_and_nothing_else() {
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
        var ordered = ids.Select(id => (id, 40_000L)).ToList();
        var allocation = CreditorPaymentWaterfall.Allocate(ordered, 80_000);
        Assert.Equal(2, allocation.Count);
        Assert.Equal(ids[0], allocation[0].InstallmentId);
        Assert.Equal(40_000, allocation[0].AmountMinorUnits);
        Assert.Equal(ids[1], allocation[1].InstallmentId);
        Assert.Equal(40_000, allocation[1].AmountMinorUnits);
    }

    [Fact]
    public void Allocate_takes_a_partly_paid_first_row_before_moving_on() {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var ordered = new[] { (firstId, 25_000L), (secondId, 40_000L) };
        var allocation = CreditorPaymentWaterfall.Allocate(ordered, 30_000);
        Assert.Equal(2, allocation.Count);
        Assert.Equal(firstId, allocation[0].InstallmentId);
        Assert.Equal(25_000, allocation[0].AmountMinorUnits);
        Assert.Equal(secondId, allocation[1].InstallmentId);
        Assert.Equal(5_000, allocation[1].AmountMinorUnits);
    }

    [Fact]
    public void Allocate_for_the_full_remaining_total_fills_every_cuota() {
        var ids = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToList();
        var ordered = ids.Select(id => (id, 10_000L)).ToList();
        var allocation = CreditorPaymentWaterfall.Allocate(ordered, 30_000);
        Assert.Equal(3, allocation.Count);
        Assert.All(allocation, entry => Assert.Equal(10_000, entry.AmountMinorUnits));
    }
}

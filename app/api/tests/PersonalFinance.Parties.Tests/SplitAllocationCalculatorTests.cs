using PersonalFinance.Parties.Application;
using Xunit;

namespace PersonalFinance.Parties.Tests;

public class SplitAllocationCalculatorTests {
    [Theory]
    [InlineData(1000, new long[] { 1 })]
    [InlineData(1000, new long[] { 1, 1 })]
    [InlineData(1001, new long[] { 1, 1 })]
    [InlineData(100000, new long[] { 1, 2, 3 })]
    [InlineData(7, new long[] { 1, 1, 1, 1 })]
    [InlineData(999999, new long[] { 5, 3, 2 })]
    public void AllocateWhole_reconciles_to_the_cent_and_never_goes_negative(long total, long[] participantWeights) {
        var shares = SplitAllocationCalculator.AllocateWhole(total, participantWeights);
        Assert.Equal(participantWeights.Length, shares.ParticipantShares.Count);
        Assert.True(shares.HolderShare >= 0);
        Assert.All(shares.ParticipantShares, share => Assert.True(share >= 0));
        Assert.Equal(total, shares.HolderShare + shares.ParticipantShares.Sum());
    }

    [Fact]
    public void AllocateWhole_gives_the_odd_cent_to_the_holder() {
        var shares = SplitAllocationCalculator.AllocateWhole(1001, [1]);
        Assert.Equal(501, shares.HolderShare);
        Assert.Equal(500, Assert.Single(shares.ParticipantShares));
    }

    [Theory]
    [InlineData(new long[] { 334, 333, 333 }, new long[] { 1 })]
    [InlineData(new long[] { 100000, 100000, 100000 }, new long[] { 1, 1, 1 })]
    [InlineData(new long[] { 1, 1, 1 }, new long[] { 1, 1 })]
    public void AllocatePerInstallment_reconciles_every_installment_exactly(long[] installmentAmounts, long[] participantWeights) {
        var perInstallment = SplitAllocationCalculator.AllocatePerInstallment(installmentAmounts, participantWeights);
        Assert.Equal(installmentAmounts.Length, perInstallment.Count);
        for(var index = 0; index < installmentAmounts.Length; index++) {
            var shares = perInstallment[index];
            Assert.Equal(installmentAmounts[index], shares.HolderShare + shares.ParticipantShares.Sum());
        }
    }

    [Fact]
    public void AllocatePerInstallment_keeps_the_grand_total_exact_with_bounded_drift_toward_the_holder() {
        long[] installmentAmounts = [334, 333, 333];
        var perInstallment = SplitAllocationCalculator.AllocatePerInstallment(installmentAmounts, [1]);
        var holderTotal = perInstallment.Sum(shares => shares.HolderShare);
        var partyTotal = perInstallment.Sum(shares => shares.ParticipantShares.Single());
        Assert.Equal(1000, holderTotal + partyTotal);
        Assert.True(holderTotal >= partyTotal, "the odd cent of each installment favours the holder");
        Assert.True(holderTotal - partyTotal <= installmentAmounts.Length, "drift is at most one minor unit per installment");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void AllocateWhole_rejects_a_non_positive_total(long total) {
        Assert.Throws<ArgumentOutOfRangeException>(() => SplitAllocationCalculator.AllocateWhole(total, [1]));
    }

    [Fact]
    public void AllocateWhole_rejects_an_empty_participant_set() {
        Assert.Throws<ArgumentException>(() => SplitAllocationCalculator.AllocateWhole(1000, []));
    }

    [Fact]
    public void AllocatePerInstallment_rejects_an_empty_installment_set() {
        Assert.Throws<ArgumentException>(() => SplitAllocationCalculator.AllocatePerInstallment([], [1]));
    }
}

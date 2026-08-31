using PersonalFinance.Parties.Domain;
using PersonalFinance.SharedKernel;
using Xunit;

namespace PersonalFinance.Parties.Tests;

public class ExpenseSplitTests {
    private static readonly Currency currency = Currency.Reference;

    [Fact]
    public void Create_records_the_shares_and_seeds_the_receivable_counters() {
        var partyId = Guid.CreateVersion7();
        var split = ExpenseSplit.Create(
            ExpenseSplitSource.Debit,
            Guid.CreateVersion7(),
            Money.FromMinorUnits(1000, currency),
            Money.FromMinorUnits(600, currency),
            [new PartyShare(partyId, Money.FromMinorUnits(400, currency))],
            Money.FromMinorUnits(400, currency)
        ).Value;

        Assert.Equal(400, split.AccruedReceivable.MinorUnits);
        Assert.Equal(0, split.ReversedReceivable.MinorUnits);
        Assert.Equal(400, split.PartyReceivableTotal.MinorUnits);
        Assert.Equal(partyId, Assert.Single(split.Participants).PartyId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_rejects_a_non_positive_total(long total) {
        var result = ExpenseSplit.Create(
            ExpenseSplitSource.Debit,
            Guid.CreateVersion7(),
            Money.FromMinorUnits(total, currency),
            Money.Zero(currency),
            [new PartyShare(Guid.CreateVersion7(), Money.Zero(currency))],
            Money.Zero(currency)
        );

        Assert.True(result.IsFailure);
        Assert.Equal(PartiesErrors.NonPositiveAmount, result.Error);
    }

    [Fact]
    public void Create_rejects_an_empty_participant_set() {
        var result = ExpenseSplit.Create(
            ExpenseSplitSource.Debit,
            Guid.CreateVersion7(),
            Money.FromMinorUnits(1000, currency),
            Money.FromMinorUnits(1000, currency),
            [],
            Money.Zero(currency)
        );

        Assert.True(result.IsFailure);
        Assert.Equal(PartiesErrors.InvalidParticipants, result.Error);
    }

    [Fact]
    public void Create_throws_when_the_shares_do_not_reconcile_to_the_total() {
        Assert.Throws<InvalidOperationException>(() => ExpenseSplit.Create(
            ExpenseSplitSource.CardPlan,
            Guid.CreateVersion7(),
            Money.FromMinorUnits(1000, currency),
            Money.FromMinorUnits(600, currency),
            [new PartyShare(Guid.CreateVersion7(), Money.FromMinorUnits(300, currency))],
            Money.Zero(currency)
        ));
    }

    [Fact]
    public void RecordAccrued_adds_to_the_accrued_receivable() {
        var split = NewSplit();

        Assert.True(split.RecordAccrued(Money.FromMinorUnits(150, currency)).IsSuccess);
        Assert.True(split.RecordAccrued(Money.FromMinorUnits(50, currency)).IsSuccess);

        Assert.Equal(200, split.AccruedReceivable.MinorUnits);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void RecordAccrued_rejects_a_non_positive_amount(long minorUnits) {
        var split = NewSplit();

        var result = split.RecordAccrued(Money.FromMinorUnits(minorUnits, currency));

        Assert.True(result.IsFailure);
        Assert.Equal(PartiesErrors.NonPositiveAmount, result.Error);
        Assert.Equal(0, split.AccruedReceivable.MinorUnits);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void RecordReversed_rejects_a_non_positive_amount(long minorUnits) {
        var split = NewSplit();

        var result = split.RecordReversed(Money.FromMinorUnits(minorUnits, currency));

        Assert.True(result.IsFailure);
        Assert.Equal(PartiesErrors.NonPositiveAmount, result.Error);
        Assert.Equal(0, split.ReversedReceivable.MinorUnits);
    }

    [Fact]
    public void An_accrued_then_fully_reversed_split_keeps_both_counters_coherent() {
        var split = NewSplit();
        split.RecordAccrued(Money.FromMinorUnits(400, currency));

        split.RecordReversed(Money.FromMinorUnits(400, currency));

        Assert.Equal(400, split.AccruedReceivable.MinorUnits);
        Assert.Equal(400, split.ReversedReceivable.MinorUnits);
        Assert.Equal(
            split.AccruedReceivable.MinorUnits,
            split.ReversedReceivable.MinorUnits);
    }

    private static ExpenseSplit NewSplit() {
        return ExpenseSplit.Create(
            ExpenseSplitSource.CardPlan,
            Guid.CreateVersion7(),
            Money.FromMinorUnits(1000, currency),
            Money.FromMinorUnits(600, currency),
            [new PartyShare(Guid.CreateVersion7(), Money.FromMinorUnits(400, currency))],
            Money.Zero(currency)
        ).Value;
    }
}

using PersonalFinance.Financing.Domain;
using PersonalFinance.SharedKernel;
using Xunit;

namespace PersonalFinance.Financing.Tests;

public class InstallmentReversalTests {
    [Fact]
    public void MarkReversed_flips_the_flag_and_succeeds() {
        var installment = Installment.Schedule(Guid.NewGuid(), 1, minorUnits(1000), new BillingCycle(2026, 3));
        var result = installment.MarkReversed();
        Assert.True(result.IsSuccess);
        Assert.True(installment.IsReversed);
    }

    [Fact]
    public void MarkReversed_a_second_time_reports_the_installment_already_reversed() {
        var installment = Installment.Schedule(Guid.NewGuid(), 1, minorUnits(1000), new BillingCycle(2026, 3));
        installment.MarkReversed();
        var second = installment.MarkReversed();
        Assert.True(second.IsFailure);
        Assert.Equal("Financing.InstallmentAlreadyReversed", second.Error.Code);
    }

    [Fact]
    public void ApplyCredit_accumulates_onto_the_carried_balance() {
        var card = newCard();
        card.ApplyCredit(minorUnits(1000));
        card.ApplyCredit(minorUnits(250));
        Assert.Equal(1250, card.CarriedCreditBalance.MinorUnits);
    }

    [Fact]
    public void ApplyCredit_rejects_a_non_positive_amount() {
        var card = newCard();
        var result = card.ApplyCredit(Money.Zero(Currency.Reference));
        Assert.True(result.IsFailure);
        Assert.Equal("Financing.NonPositiveCreditAmount", result.Error.Code);
        Assert.Equal(0, card.CarriedCreditBalance.MinorUnits);
    }

    [Fact]
    public void ConsumeCredit_subtracts_from_the_carried_balance() {
        var card = newCard();
        card.ApplyCredit(minorUnits(1000));
        card.ConsumeCredit(minorUnits(400));
        Assert.Equal(600, card.CarriedCreditBalance.MinorUnits);
    }

    [Fact]
    public void ConsumeCredit_clamps_at_zero_when_consuming_more_than_is_carried() {
        var card = newCard();
        card.ApplyCredit(minorUnits(300));

        card.ConsumeCredit(minorUnits(500));

        Assert.Equal(0, card.CarriedCreditBalance.MinorUnits);
    }

    [Fact]
    public void ConsumeCredit_rejects_a_non_positive_amount() {
        var card = newCard();
        card.ApplyCredit(minorUnits(1000));
        var result = card.ConsumeCredit(minorUnits(-100));
        Assert.True(result.IsFailure);
        Assert.Equal("Financing.NonPositiveCreditAmount", result.Error.Code);
        Assert.Equal(1000, card.CarriedCreditBalance.MinorUnits);
    }

    private static CreditCard newCard() {
        return CreditCard.Create(Guid.NewGuid(), "Visa", 15, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()).Value;
    }

    private static Money minorUnits(long amount) {
        return Money.FromMinorUnits(amount, Currency.Reference);
    }
}

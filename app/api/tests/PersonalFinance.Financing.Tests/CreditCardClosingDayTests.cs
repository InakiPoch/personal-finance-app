using PersonalFinance.Financing.Domain;
using PersonalFinance.SharedKernel;
using PersonalFinance.SharedKernel.Allocation;
using Xunit;

namespace PersonalFinance.Financing.Tests;

public class CreditCardClosingDayTests {
    private static readonly BillingCycle october = new(2026, 10);

    [Fact]
    public void Closing_day_without_an_override_is_the_usual_day() {
        var card = newCard(28);
        Assert.Equal(28, card.ClosingDayOf(october));
        Assert.Equal(new DateOnly(2026, 10, 28), card.ClosingDateOf(october));
        Assert.False(card.HasOverrideFor(october));
    }

    [Fact]
    public void Override_wins_for_its_month_only() {
        var card = newCard(28);
        Assert.True(card.SetClosingDay(october, 24).IsSuccess);
        Assert.Equal(24, card.ClosingDayOf(october));
        Assert.True(card.HasOverrideFor(october));
        Assert.Equal(28, card.ClosingDayOf(october.AddMonths(1)));
    }

    [Fact]
    public void Setting_the_same_month_twice_replaces_the_override() {
        var card = newCard(28);
        card.SetClosingDay(october, 24);
        card.SetClosingDay(october, 20);
        Assert.Single(card.ClosingOverrides);
        Assert.Equal(20, card.ClosingDayOf(october));
    }

    [Fact]
    public void Usual_day_beyond_the_month_length_clamps_to_the_last_day() {
        var card = newCard(31);
        Assert.Equal(28, card.ClosingDayOf(new BillingCycle(2026, 2)));
        Assert.Equal(new DateOnly(2026, 2, 28), card.ClosingDateOf(new BillingCycle(2026, 2)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    [InlineData(32)]
    public void SetClosingDay_rejects_a_day_outside_the_month(int day) {
        var card = newCard(15);
        var result = card.SetClosingDay(new BillingCycle(2026, 4), day);
        Assert.True(result.IsFailure);
        Assert.Equal("Financing.InvalidClosingDay", result.Error.Code);
        Assert.Empty(card.ClosingOverrides);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    public void ChangeUsualClosingDay_rejects_a_day_outside_one_to_thirty_one(int day) {
        var card = newCard(15);
        var result = card.ChangeUsualClosingDay(day);
        Assert.True(result.IsFailure);
        Assert.Equal(15, card.CutoffDay);
    }

    [Fact]
    public void ChangeUsualClosingDay_updates_the_cutoff() {
        var card = newCard(15);
        Assert.True(card.ChangeUsualClosingDay(31).IsSuccess);
        Assert.Equal(31, card.CutoffDay);
    }

    [Fact]
    public void ClearClosingDay_falls_back_to_the_usual_day() {
        var card = newCard(28);
        card.SetClosingDay(october, 24);
        Assert.True(card.ClearClosingDay(october).IsSuccess);
        Assert.False(card.HasOverrideFor(october));
        Assert.Equal(28, card.ClosingDayOf(october));
    }

    [Fact]
    public void ResolveCycle_with_an_earlier_override_pushes_the_purchase_to_the_next_month() {
        var card = newCard(28);
        Assert.Equal(october, card.ResolveCycle(new DateOnly(2026, 10, 25)));
        card.SetClosingDay(october, 24);
        Assert.Equal(new BillingCycle(2026, 11), card.ResolveCycle(new DateOnly(2026, 10, 25)));
    }

    [Fact]
    public void ResolveCycle_with_a_later_override_keeps_the_purchase_in_the_month() {
        var card = newCard(15);
        Assert.Equal(new BillingCycle(2026, 11), card.ResolveCycle(new DateOnly(2026, 10, 25)));
        card.SetClosingDay(october, 30);
        Assert.Equal(october, card.ResolveCycle(new DateOnly(2026, 10, 25)));
    }

    [Fact]
    public void IsClosedAsOf_uses_the_override_date() {
        var card = newCard(28);
        card.SetClosingDay(october, 20);
        Assert.False(card.IsClosedAsOf(october, new DateOnly(2026, 10, 20)));
        Assert.True(card.IsClosedAsOf(october, new DateOnly(2026, 10, 21)));
    }

    [Fact]
    public void Rebucket_shifts_a_plan_forward_when_the_closing_moves_earlier() {
        var card = newCard(28);
        var plan = newPlan(card, new DateOnly(2026, 10, 25));
        assertCycles(plan, (2026, 10), (2026, 11), (2026, 12));
        card.SetClosingDay(october, 24);
        Assert.True(CardRebucketer.Rebucket(card, [plan]).IsSuccess);
        assertCycles(plan, (2026, 11), (2026, 12), (2027, 1));
    }

    [Fact]
    public void Rebucket_shifts_a_plan_back_when_the_closing_moves_later() {
        var card = newCard(28);
        card.SetClosingDay(october, 24);
        var plan = newPlan(card, new DateOnly(2026, 10, 25));
        CardRebucketer.Rebucket(card, [plan]);
        card.SetClosingDay(october, 30);
        Assert.True(CardRebucketer.Rebucket(card, [plan]).IsSuccess);
        assertCycles(plan, (2026, 10), (2026, 11), (2026, 12));
    }

    [Fact]
    public void Rebucket_leaves_unaffected_plans_alone() {
        var card = newCard(28);
        var plan = newPlan(card, new DateOnly(2026, 10, 10));
        card.SetClosingDay(october, 24);
        Assert.True(CardRebucketer.Rebucket(card, [plan]).IsSuccess);
        assertCycles(plan, (2026, 10), (2026, 11), (2026, 12));
    }

    [Fact]
    public void Rebucket_refuses_a_plan_with_a_charged_installment_and_moves_nothing() {
        var card = newCard(28);
        var charged = newPlan(card, new DateOnly(2026, 10, 25));
        var other = newPlan(card, new DateOnly(2026, 10, 26));
        var statement = MonthlyStatement.Open(card.Id, new BillingCycle(2026, 9), Currency.Reference);
        charged.Installments.OrderBy(installment => installment.Sequence).First().MarkAccrued(DateTimeOffset.UtcNow, statement);
        card.SetClosingDay(october, 24);
        var result = CardRebucketer.Rebucket(card, [other, charged]);
        Assert.True(result.IsFailure);
        Assert.Equal("Financing.ClosingChangeMovesChargedPurchase", result.Error.Code);
        assertCycles(other, (2026, 10), (2026, 11), (2026, 12));
        assertCycles(charged, (2026, 10), (2026, 11), (2026, 12));
    }

    private static CreditCard newCard(int cutoffDay) {
        return CreditCard.Create(Guid.CreateVersion7(), "Visa", cutoffDay, Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7()).Value;
    }

    private static PaymentPlan newPlan(CreditCard card, DateOnly purchaseDate) {
        return PaymentPlan.Create(card.Id, Money.FromMinorUnits(9_000, Currency.Reference), 3, purchaseDate, "Plan", card.CutoffDay, new PhantomPennyAllocator()).Value;
    }

    private static void assertCycles(PaymentPlan plan, params (int Year, int Month)[] expected) {
        var actual = plan.Installments.OrderBy(installment => installment.Sequence).Select(installment => (installment.CycleYear, installment.CycleMonth)).ToArray();
        Assert.Equal(expected, actual);
    }
}

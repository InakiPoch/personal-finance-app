using PersonalFinance.SharedKernel;
using PersonalFinance.Subscriptions.Contracts;
using PersonalFinance.Subscriptions.Domain;
using Xunit;

namespace PersonalFinance.Subscriptions.Tests;

public class SubscriptionTemplateTests {
    [Fact]
    public void Create_returns_an_active_template_with_no_period_paid_yet() {
        var template = CreateTemplate().Value;

        Assert.True(template.IsActive);
        Assert.Equal(new DateOnly(2026, 3, 15), template.NextDueDate);
        Assert.Null(template.LastPaidPeriod);
    }

    [Fact]
    public void Create_trims_the_name_and_category() {
        var template = CreateTemplate(name: "  Netflix  ", category: "  Streaming  ").Value;

        Assert.Equal("Netflix", template.Name);
        Assert.Equal("Streaming", template.Category);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_a_blank_name(string name) {
        var result = CreateTemplate(name: name);
        Assert.True(result.IsFailure);
        Assert.Equal(SubscriptionErrors.InvalidName, result.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_a_blank_category(string category) {
        var result = CreateTemplate(category: category);
        Assert.True(result.IsFailure);
        Assert.Equal(SubscriptionErrors.InvalidCategory, result.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_rejects_a_non_positive_amount(long minorUnits) {
        var result = CreateTemplate(amount: Money.FromMinorUnits(minorUnits, Currency.Reference));
        Assert.True(result.IsFailure);
        Assert.Equal(SubscriptionErrors.NonPositiveAmount, result.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    public void Create_rejects_an_anchor_day_outside_the_valid_range(int anchorDay) {
        var result = CreateTemplate(anchorDay: anchorDay);
        Assert.True(result.IsFailure);
        Assert.Equal(SubscriptionErrors.InvalidAnchorDay, result.Error);
    }

    [Fact]
    public void Create_rejects_an_empty_expense_account() {
        var result = CreateTemplate(expenseAccountId: Guid.Empty);
        Assert.True(result.IsFailure);
        Assert.Equal(SubscriptionErrors.InvalidFundingAccount, result.Error);
    }

    [Fact]
    public void Create_rejects_an_empty_funding_account() {
        var result = CreateTemplate(fundingAccountId: Guid.Empty);
        Assert.True(result.IsFailure);
        Assert.Equal(SubscriptionErrors.InvalidFundingAccount, result.Error);
    }

    [Fact]
    public void MarkCurrentPeriodPaid_stamps_the_period_and_advances_the_due_date_by_one_period() {
        var template = CreateTemplate(anchorDay: 15).Value;

        var result = template.MarkCurrentPeriodPaid(new DateOnly(2026, 3, 15));

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2026, 3, 15), template.LastPaidPeriod);
        Assert.Equal(new DateOnly(2026, 4, 15), template.NextDueDate);
    }

    [Fact]
    public void MarkCurrentPeriodPaid_after_cancellation_fails_and_leaves_state_untouched() {
        var template = CreateTemplate(anchorDay: 15).Value;
        template.Cancel();

        var result = template.MarkCurrentPeriodPaid(new DateOnly(2026, 3, 15));

        Assert.True(result.IsFailure);
        Assert.Equal(SubscriptionErrors.SubscriptionNotActive, result.Error);
        Assert.Null(template.LastPaidPeriod);
        Assert.Equal(new DateOnly(2026, 3, 15), template.NextDueDate);
    }

    [Fact]
    public void RevertLastPayment_steps_the_paid_period_and_due_date_back_one_month() {
        var template = CreateTemplate(anchorDay: 15).Value;
        template.MarkCurrentPeriodPaid(new DateOnly(2026, 3, 15));

        var result = template.RevertLastPayment();

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2026, 2, 15), template.LastPaidPeriod);
        Assert.Equal(new DateOnly(2026, 3, 15), template.NextDueDate);
    }

    [Fact]
    public void RevertLastPayment_when_never_paid_leaves_last_paid_period_null_but_still_steps_the_due_date_back() {
        var template = CreateTemplate(anchorDay: 15).Value;

        var result = template.RevertLastPayment();

        Assert.True(result.IsSuccess);
        Assert.Null(template.LastPaidPeriod);
        Assert.Equal(new DateOnly(2026, 2, 15), template.NextDueDate);
    }

    [Fact]
    public void RevertLastPayment_after_cancellation_fails_and_leaves_state_untouched() {
        var template = CreateTemplate(anchorDay: 15).Value;
        template.MarkCurrentPeriodPaid(new DateOnly(2026, 3, 15));
        template.Cancel();

        var result = template.RevertLastPayment();

        Assert.True(result.IsFailure);
        Assert.Equal(SubscriptionErrors.SubscriptionNotActive, result.Error);
        Assert.Equal(new DateOnly(2026, 3, 15), template.LastPaidPeriod);
        Assert.Equal(new DateOnly(2026, 4, 15), template.NextDueDate);
    }

    [Fact]
    public void Cancel_is_idempotent_and_deactivates_the_template() {
        var template = CreateTemplate().Value;

        var first = template.Cancel();
        var second = template.Cancel();

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.False(template.IsActive);
    }

    [Fact]
    public void A_cancelled_template_reports_itself_inactive() {
        var template = CreateTemplate().Value;
        template.Cancel();

        Assert.False(template.IsActive);
        Assert.False(template.Schedule.IsActive);
    }

    private static Result<SubscriptionTemplate> CreateTemplate(
        string name = "Netflix",
        Money? amount = null,
        string category = "Streaming",
        Guid? expenseAccountId = null,
        Guid? fundingAccountId = null,
        int anchorDay = 15) {
        return SubscriptionTemplate.Create(
            name,
            amount ?? Money.FromMinorUnits(1_500, Currency.Reference),
            category,
            expenseAccountId ?? Guid.CreateVersion7(),
            fundingAccountId ?? Guid.CreateVersion7(),
            RecurrenceFrequency.Monthly,
            anchorDay,
            new DateOnly(2026, 3, 15)
        );
    }
}

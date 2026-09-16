using PersonalFinance.SharedKernel;
using PersonalFinance.Subscriptions.Contracts;
using PersonalFinance.Subscriptions.Domain;
using Xunit;

namespace PersonalFinance.Subscriptions.Tests;

public class SubscriptionTemplateTests {
    private static readonly DateTimeOffset FirstChargeOn = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_returns_an_active_template_stamped_with_the_first_charge_moment() {
        var template = CreateTemplate().Value;

        Assert.True(template.IsActive);
        Assert.Equal(new DateOnly(2026, 3, 15), template.NextDueDate);
        Assert.Equal(FirstChargeOn, template.LastRenewalOnUtc);
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
            new DateOnly(2026, 3, 15),
            FirstChargeOn
        );
    }
}

using PersonalFinance.Subscriptions.Contracts;
using PersonalFinance.Subscriptions.Domain;
using Xunit;

namespace PersonalFinance.Subscriptions.Tests;

public class RecurrenceRuleTests {
    [Fact]
    public void Next_returns_the_anchor_day_in_the_same_month_when_it_is_still_ahead() {
        var rule = Rule(anchorDay: 15);
        Assert.Equal(new DateOnly(2026, 3, 15), rule.Next(new DateOnly(2026, 3, 10)));
    }

    [Fact]
    public void Next_rolls_to_the_following_month_when_asked_on_the_anchor_day_itself() {
        var rule = Rule(anchorDay: 15);
        Assert.Equal(new DateOnly(2026, 4, 15), rule.Next(new DateOnly(2026, 3, 15)));
    }

    [Fact]
    public void Next_rolls_to_the_following_month_when_the_anchor_day_has_passed() {
        var rule = Rule(anchorDay: 15);
        Assert.Equal(new DateOnly(2026, 4, 15), rule.Next(new DateOnly(2026, 3, 20)));
    }

    [Fact]
    public void Next_clamps_a_31st_anchor_to_the_last_day_of_a_non_leap_february() {
        var rule = Rule(anchorDay: 31);
        Assert.Equal(new DateOnly(2026, 2, 28), rule.Next(new DateOnly(2026, 1, 31)));
    }

    [Fact]
    public void Next_clamps_a_31st_anchor_to_the_29th_of_a_leap_february() {
        var rule = Rule(anchorDay: 31);
        Assert.Equal(new DateOnly(2028, 2, 29), rule.Next(new DateOnly(2028, 1, 31)));
    }

    [Fact]
    public void Next_clamps_a_31st_anchor_to_the_30th_of_a_thirty_day_month() {
        var rule = Rule(anchorDay: 31);
        Assert.Equal(new DateOnly(2026, 4, 30), rule.Next(new DateOnly(2026, 3, 31)));
    }

    [Fact]
    public void Next_clamps_a_29th_anchor_inside_a_non_leap_february() {
        var rule = Rule(anchorDay: 29);
        Assert.Equal(new DateOnly(2026, 2, 28), rule.Next(new DateOnly(2026, 2, 1)));
    }

    [Fact]
    public void Next_keeps_a_29th_anchor_on_the_29th_of_a_leap_february() {
        var rule = Rule(anchorDay: 29);
        Assert.Equal(new DateOnly(2028, 2, 29), rule.Next(new DateOnly(2028, 2, 1)));
    }

    [Fact]
    public void Next_rolls_past_a_clamped_february_anchor_into_march() {
        var rule = Rule(anchorDay: 29);
        Assert.Equal(new DateOnly(2026, 3, 29), rule.Next(new DateOnly(2026, 2, 28)));
    }

    [Fact]
    public void Next_rolls_across_the_year_boundary_from_december_into_january() {
        var rule = Rule(anchorDay: 10);
        Assert.Equal(new DateOnly(2027, 1, 10), rule.Next(new DateOnly(2026, 12, 15)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    [InlineData(-1)]
    public void Create_rejects_an_anchor_day_outside_the_one_to_thirty_one_range(int anchorDay) {
        var result = RecurrenceRule.Create(RecurrenceFrequency.Monthly, anchorDay);
        Assert.True(result.IsFailure);
        Assert.Equal(SubscriptionErrors.InvalidAnchorDay, result.Error);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(31)]
    public void Create_accepts_any_anchor_day_between_one_and_thirty_one(int anchorDay) {
        var result = RecurrenceRule.Create(RecurrenceFrequency.Monthly, anchorDay);
        Assert.True(result.IsSuccess);
        Assert.Equal(anchorDay, result.Value.AnchorDay);
    }

    private static RecurrenceRule Rule(int anchorDay) {
        return RecurrenceRule.Create(RecurrenceFrequency.Monthly, anchorDay).Value;
    }
}

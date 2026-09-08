using PersonalFinance.Financing.Application.Commands.CreatePaymentPlan;
using PersonalFinance.Financing.Contracts.Commands;
using Xunit;

namespace PersonalFinance.Financing.Tests;

public class CreatePaymentPlanValidatorTests {
    private static readonly DateOnly today = new(2026, 6, 1);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_rejects_a_blank_description(string description) {
        var result = CreatePaymentPlanValidator.Validate(validCommand(description), today);
        Assert.True(result.IsFailure);
        Assert.Equal("Financing.BlankDescription", result.Error.Code);
    }

    [Fact]
    public void Validate_rejects_a_description_over_120_characters() {
        var result = CreatePaymentPlanValidator.Validate(validCommand(new string('a', 121)), today);
        Assert.True(result.IsFailure);
        Assert.Equal("Financing.DescriptionTooLong", result.Error.Code);
    }

    [Theory]
    [InlineData("Line one\nLine two")]
    [InlineData("Line one\rLine two")]
    public void Validate_rejects_a_multiline_description(string description) {
        var result = CreatePaymentPlanValidator.Validate(validCommand(description), today);
        Assert.True(result.IsFailure);
        Assert.Equal("Financing.DescriptionMustBeSingleLine", result.Error.Code);
    }

    [Fact]
    public void Validate_accepts_a_valid_single_line_description() {
        var result = CreatePaymentPlanValidator.Validate(validCommand("New laptop"), today);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_rejects_a_purchase_date_in_the_future() {
        var command = new CreatePaymentPlanCommand(
            10000, Guid.CreateVersion7(), 3, today.AddDays(1), "New laptop");
        var result = CreatePaymentPlanValidator.Validate(command, today);
        Assert.True(result.IsFailure);
        Assert.Equal("Financing.FuturePurchaseDate", result.Error.Code);
    }

    [Fact]
    public void Validate_accepts_a_purchase_date_of_today() {
        var command = new CreatePaymentPlanCommand(
            10000, Guid.CreateVersion7(), 3, today, "New laptop");
        var result = CreatePaymentPlanValidator.Validate(command, today);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Validate_rejects_a_plan_that_names_both_a_card_and_a_creditor() {
        var command = new CreatePaymentPlanCommand(
            10000, Guid.CreateVersion7(), 3, new DateOnly(2026, 1, 10), "New laptop",
            CreditorId: Guid.CreateVersion7(), CreditorAccountId: Guid.CreateVersion7());
        var result = CreatePaymentPlanValidator.Validate(command, today);
        Assert.True(result.IsFailure);
        Assert.Equal("Financing.PlanCannotMixCardAndCreditor", result.Error.Code);
    }

    [Fact]
    public void Validate_rejects_a_plan_that_names_neither_a_card_nor_a_creditor() {
        var command = new CreatePaymentPlanCommand(10000, CardId: null, 3, new DateOnly(2026, 1, 10), "New laptop");
        var result = CreatePaymentPlanValidator.Validate(command, today);
        Assert.True(result.IsFailure);
        Assert.Equal("Financing.PlanNeedsCardOrCreditor", result.Error.Code);
    }

    [Fact]
    public void Validate_rejects_a_creditor_plan_with_no_account_to_pay() {
        var command = new CreatePaymentPlanCommand(
            10000, CardId: null, 3, new DateOnly(2026, 1, 10), "New laptop",
            CreditorId: Guid.CreateVersion7());
        var result = CreatePaymentPlanValidator.Validate(command, today);
        Assert.True(result.IsFailure);
        Assert.Equal("Financing.CreditorAccountRequired", result.Error.Code);
    }

    private static CreatePaymentPlanCommand validCommand(string description) {
        return new CreatePaymentPlanCommand(10000, Guid.CreateVersion7(), 3, new DateOnly(2026, 1, 10), description);
    }
}

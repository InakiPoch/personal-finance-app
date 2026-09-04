using PersonalFinance.Financing.Application.Commands.CreatePaymentPlan;
using PersonalFinance.Financing.Contracts.Commands;
using Xunit;

namespace PersonalFinance.Financing.Tests;

public class CreatePaymentPlanValidatorTests {
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_rejects_a_blank_description(string description) {
        var result = CreatePaymentPlanValidator.Validate(validCommand(description));
        Assert.True(result.IsFailure);
        Assert.Equal("Financing.BlankDescription", result.Error.Code);
    }

    [Fact]
    public void Validate_rejects_a_description_over_120_characters() {
        var result = CreatePaymentPlanValidator.Validate(validCommand(new string('a', 121)));
        Assert.True(result.IsFailure);
        Assert.Equal("Financing.DescriptionTooLong", result.Error.Code);
    }

    [Theory]
    [InlineData("Line one\nLine two")]
    [InlineData("Line one\rLine two")]
    public void Validate_rejects_a_multiline_description(string description) {
        var result = CreatePaymentPlanValidator.Validate(validCommand(description));
        Assert.True(result.IsFailure);
        Assert.Equal("Financing.DescriptionMustBeSingleLine", result.Error.Code);
    }

    [Fact]
    public void Validate_accepts_a_valid_single_line_description() {
        var result = CreatePaymentPlanValidator.Validate(validCommand("New laptop"));
        Assert.True(result.IsSuccess);
    }

    private static CreatePaymentPlanCommand validCommand(string description) {
        return new CreatePaymentPlanCommand(10000, Guid.CreateVersion7(), 3, new DateOnly(2026, 1, 10), description);
    }
}

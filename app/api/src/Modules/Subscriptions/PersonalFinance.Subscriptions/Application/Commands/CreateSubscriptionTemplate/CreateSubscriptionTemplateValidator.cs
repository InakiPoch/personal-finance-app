using PersonalFinance.SharedKernel;
using PersonalFinance.Subscriptions.Contracts.Commands;
using PersonalFinance.Subscriptions.Domain;

namespace PersonalFinance.Subscriptions.Application.Commands.CreateSubscriptionTemplate;

internal static class CreateSubscriptionTemplateValidator {
    public static Result Validate(CreateSubscriptionTemplateCommand command) {
        if(string.IsNullOrWhiteSpace(command.Name)) {
            return Result.Failure(SubscriptionErrors.InvalidName);
        }
        if(string.IsNullOrWhiteSpace(command.Category)) {
            return Result.Failure(SubscriptionErrors.InvalidCategory);
        }
        if(command.AmountMinorUnits <= 0) {
            return Result.Failure(SubscriptionErrors.NonPositiveAmount);
        }
        if(command.AnchorDay is < 1 or > 31) {
            return Result.Failure(SubscriptionErrors.InvalidAnchorDay);
        }
        if(command.CurrencyCode is not ("ARS" or "USD")) {
            return Result.Failure(SubscriptionErrors.InvalidCurrencyCode);
        }
        return command.FundingAccountId == Guid.Empty
            ? Result.Failure(SubscriptionErrors.InvalidFundingAccount)
        : Result.Success();
    }
}

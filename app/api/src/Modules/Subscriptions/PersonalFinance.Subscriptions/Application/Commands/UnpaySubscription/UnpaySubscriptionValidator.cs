using PersonalFinance.SharedKernel;
using PersonalFinance.Subscriptions.Contracts.Commands;
using PersonalFinance.Subscriptions.Domain;

namespace PersonalFinance.Subscriptions.Application.Commands.UnpaySubscription;

internal static class UnpaySubscriptionValidator {
    public static Result Validate(UnpaySubscriptionCommand command) {
        return command.SubscriptionId == Guid.Empty
            ? Result.Failure(SubscriptionErrors.SubscriptionNotFound)
        : Result.Success();
    }
}

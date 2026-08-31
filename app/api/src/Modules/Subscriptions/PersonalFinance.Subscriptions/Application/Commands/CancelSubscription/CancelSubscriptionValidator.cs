using PersonalFinance.SharedKernel;
using PersonalFinance.Subscriptions.Contracts.Commands;
using PersonalFinance.Subscriptions.Domain;

namespace PersonalFinance.Subscriptions.Application.Commands.CancelSubscription;

internal static class CancelSubscriptionValidator {
    public static Result Validate(CancelSubscriptionCommand command) {
        return command.SubscriptionId == Guid.Empty
            ? Result.Failure(SubscriptionErrors.SubscriptionNotFound)
        : Result.Success();
    }
}

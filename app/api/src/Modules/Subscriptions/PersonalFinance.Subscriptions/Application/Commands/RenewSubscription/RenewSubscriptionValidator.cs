using PersonalFinance.SharedKernel;
using PersonalFinance.Subscriptions.Contracts.Commands;
using PersonalFinance.Subscriptions.Domain;

namespace PersonalFinance.Subscriptions.Application.Commands.RenewSubscription;

internal static class RenewSubscriptionValidator {
    public static Result Validate(RenewSubscriptionCommand command) {
        return command.SubscriptionId == Guid.Empty
            ? Result.Failure(SubscriptionErrors.SubscriptionNotFound)
        : Result.Success();
    }
}

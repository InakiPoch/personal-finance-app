using PersonalFinance.SharedKernel;
using PersonalFinance.Subscriptions.Contracts.Commands;
using PersonalFinance.Subscriptions.Domain;

namespace PersonalFinance.Subscriptions.Application.Commands.PaySubscription;

internal static class PaySubscriptionValidator {
    public static Result Validate(PaySubscriptionCommand command) {
        return command.SubscriptionId == Guid.Empty
            ? Result.Failure(SubscriptionErrors.SubscriptionNotFound)
        : Result.Success();
    }
}

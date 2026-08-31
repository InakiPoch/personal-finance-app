using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.SharedKernel;
using PersonalFinance.Subscriptions.Contracts;
using PersonalFinance.Subscriptions.Contracts.Commands;
using PersonalFinance.Subscriptions.Contracts.Queries;

namespace PersonalFinance.Subscriptions.Infrastructure.PublicApi;

internal sealed class SubscriptionsApi(ICommandBus commandBus, IQueryBus queryBus) : ISubscriptionsApi {
    public Task<Result<Guid>> CreateSubscriptionTemplateAsync(CreateSubscriptionTemplateCommand command, CancellationToken ct = default) {
        return commandBus.SendAsync(command, ct);
    }

    public Task<Result<Guid>> RenewSubscriptionAsync(RenewSubscriptionCommand command, CancellationToken ct = default) {
        return commandBus.SendAsync(command, ct);
    }

    public Task<Result> CancelSubscriptionAsync(CancelSubscriptionCommand command, CancellationToken ct = default) {
        return commandBus.SendAsync(command, ct);
    }

    public Task<ActiveSubscriptionsResponse> GetActiveSubscriptionsAsync(GetActiveSubscriptionsQuery query, CancellationToken ct = default) {
        return queryBus.AskAsync(query, ct);
    }
}

using PersonalFinance.SharedKernel;
using PersonalFinance.Subscriptions.Contracts.Commands;
using PersonalFinance.Subscriptions.Contracts.Queries;

namespace PersonalFinance.Subscriptions.Contracts;

public interface ISubscriptionsApi {
    Task<Result<Guid>> CreateSubscriptionTemplateAsync(CreateSubscriptionTemplateCommand command, CancellationToken ct = default);
    Task<Result<Guid>> RenewSubscriptionAsync(RenewSubscriptionCommand command, CancellationToken ct = default);
    Task<Result> CancelSubscriptionAsync(CancelSubscriptionCommand command, CancellationToken ct = default);
    Task<ActiveSubscriptionsResponse> GetActiveSubscriptionsAsync(GetActiveSubscriptionsQuery query, CancellationToken ct = default);
}

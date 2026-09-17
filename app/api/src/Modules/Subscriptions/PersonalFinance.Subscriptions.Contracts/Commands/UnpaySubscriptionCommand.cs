using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Subscriptions.Contracts.Commands;

public sealed record UnpaySubscriptionCommand(Guid SubscriptionId) : ICommand<Guid>;

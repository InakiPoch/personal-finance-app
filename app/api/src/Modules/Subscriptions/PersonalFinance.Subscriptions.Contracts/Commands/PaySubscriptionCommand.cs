using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Subscriptions.Contracts.Commands;

public sealed record PaySubscriptionCommand(Guid SubscriptionId) : ICommand<Guid>;

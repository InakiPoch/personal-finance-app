using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Subscriptions.Contracts.Commands;

public sealed record CancelSubscriptionCommand(Guid SubscriptionId) : ICommand;

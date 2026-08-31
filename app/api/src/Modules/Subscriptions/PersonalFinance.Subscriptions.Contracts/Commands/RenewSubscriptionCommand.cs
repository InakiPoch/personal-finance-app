using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Subscriptions.Contracts.Commands;

public sealed record RenewSubscriptionCommand(Guid SubscriptionId, DateTimeOffset RenewedOnUtc) : ICommand<Guid>;

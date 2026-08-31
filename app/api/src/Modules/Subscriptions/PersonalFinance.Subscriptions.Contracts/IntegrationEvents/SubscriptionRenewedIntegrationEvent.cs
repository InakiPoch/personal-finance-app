using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Subscriptions.Contracts.IntegrationEvents;

/// <summary>
/// Announced after a subscription renewal posts its period charge.
/// </summary>
public sealed record SubscriptionRenewedIntegrationEvent(
    Guid MessageId,
    DateTimeOffset OccurredOnUtc,
    Guid SubscriptionId,
    Guid TransactionId,
    DateOnly RenewedDueDate
) : IIntegrationEvent;

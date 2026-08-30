using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Subscriptions.Contracts.Queries;

public sealed record ActiveSubscriptionRow(
    Guid SubscriptionId,
    string Name,
    long AmountMinorUnits,
    string Category,
    RecurrenceFrequency Frequency,
    int AnchorDay,
    DateOnly NextDueDate
);

/// <summary>
/// The subscriptions still renewing.
/// </summary>
public sealed record ActiveSubscriptionsResponse(IReadOnlyList<ActiveSubscriptionRow> Rows);

public sealed record GetActiveSubscriptionsQuery() : IQuery<ActiveSubscriptionsResponse>;

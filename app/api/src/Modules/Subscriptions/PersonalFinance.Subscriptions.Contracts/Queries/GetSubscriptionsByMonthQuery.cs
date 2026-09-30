using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Subscriptions.Contracts.Queries;

public sealed record SubscriptionByMonthRow(
    Guid SubscriptionId,
    string Name,
    long AmountMinorUnits,
    string Category,
    RecurrenceFrequency Frequency,
    int AnchorDay,
    DateOnly NextDueDate,
    DateOnly DueDate,
    string Status,
    string CurrencyCode
);

/// <summary>
/// The active subscriptions that renew inside the requested month, each with its occurrence date and paid/overdue/upcoming status.
/// </summary>
public sealed record SubscriptionsByMonthResponse(IReadOnlyList<SubscriptionByMonthRow> Rows);

/// <summary>
/// <paramref name="Month"/> is any date inside the requested calendar month.
/// </summary>
public sealed record GetSubscriptionsByMonthQuery(DateOnly Month) : IQuery<SubscriptionsByMonthResponse>;

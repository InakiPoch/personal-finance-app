using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Subscriptions.Contracts.Commands;

/// <summary>
/// Defines a recurring charge. <paramref name="AnchorDay"/> is the day-of-month it recurs on
/// (1–31, clamped to month length). The first period is charged only if the anchor day has
/// already passed, or is today, this month — otherwise it starts upcoming and unpaid.
/// </summary>
public sealed record CreateSubscriptionTemplateCommand(
    string Name,
    long AmountMinorUnits,
    string Category,
    Guid FundingAccountId,
    RecurrenceFrequency Frequency,
    int AnchorDay
) : ICommand<Guid>;

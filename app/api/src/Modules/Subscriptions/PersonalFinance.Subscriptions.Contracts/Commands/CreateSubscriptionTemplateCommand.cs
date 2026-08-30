using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Subscriptions.Contracts.Commands;

/// <summary>
/// Defines a recurring charge and posts its first period immediately.
/// <paramref name="AnchorDay"/> is the day-of-month the charge recurs on (1–31, clamped to
/// month length); every subsequent period is owned by the renewal scheduler.
/// </summary>
public sealed record CreateSubscriptionTemplateCommand(
    string Name,
    long AmountMinorUnits,
    string Category,
    Guid FundingAccountId,
    RecurrenceFrequency Frequency,
    int AnchorDay
) : ICommand<Guid>;

using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Parties.Contracts.IntegrationEvents;

public sealed record ExpenseSplitSettledIntegrationEvent(
    Guid MessageId,
    DateTimeOffset OccurredOnUtc,
    Guid PartyId,
    long AmountMinorUnits,
    Guid LedgerTransactionId
) : IIntegrationEvent;

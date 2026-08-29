using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Ledger.Contracts.IntegrationEvents;

/// <summary>
/// Announced after a ledger transaction is persisted.
/// </summary>
public sealed record TransactionPostedIntegrationEvent(Guid MessageId, DateTimeOffset OccurredOnUtc, Guid TransactionId, bool IsReversal, Guid? OriginalTransactionId) : IIntegrationEvent;
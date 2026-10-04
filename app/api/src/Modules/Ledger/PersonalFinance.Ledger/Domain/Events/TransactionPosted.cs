using PersonalFinance.SharedKernel;

namespace PersonalFinance.Ledger.Domain.Events;

/// <summary>
/// Raised when a transaction is posted or reversed. Translated to an integration event on persist.
/// </summary>
internal sealed record TransactionPosted(Guid TransactionId, DateTimeOffset PostedOnUtc, bool IsReversal) : IDomainEvent;

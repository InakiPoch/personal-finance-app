using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Ledger.Contracts.Commands;

/// <summary>
/// Reverses a posted transaction.
/// </summary>
public sealed record ReverseTransactionCommand(Guid OriginalTransactionId, DateTimeOffset ReversedOnUtc) : ICommand<Guid>;

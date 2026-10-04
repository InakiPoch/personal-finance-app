using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Ledger.Contracts.Commands;

/// <summary>
/// Reverses a posted transaction. Always succeeds with a storno; when the reversed transaction is a paid installment accrual, a compensating card-credit entry is posted too.
/// </summary>
public sealed record ReverseTransactionCommand(Guid OriginalTransactionId, DateTimeOffset ReversedOnUtc) : ICommand<ReverseTransactionResult>;

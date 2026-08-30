using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Ledger.Contracts.Commands;

/// <summary>
/// One leg of a <see cref="PostTransactionCommand"/>.
/// </summary>
public sealed record PostTransactionLine(Guid AccountId, DebitOrCredit Direction, Money Amount);

/// <summary>
/// Posts a balanced, append-only double-entry transaction to the ledger.
/// </summary>
public sealed record PostTransactionCommand(IReadOnlyList<PostTransactionLine> Lines, DateTimeOffset PostedOnUtc, Guid? SplitReferenceId = null, Guid? InstallmentReferenceId = null, string? Description = null) : ICommand<Guid>;

using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Ledger.Contracts.Commands;

/// <summary>
/// One leg of a <see cref="PostTransactionCommand"/>.
/// </summary>
public sealed record PostTransactionLine(Guid AccountId, DebitOrCredit Direction, Money Amount);

public sealed record PostTransactionCommand(
    IReadOnlyList<PostTransactionLine> Lines, 
    DateTimeOffset PostedOnUtc, 
    Guid? SplitReferenceId = null, 
    Guid? InstallmentReferenceId = null, 
    Guid? SubscriptionReferenceId = null, 
    string? Description = null
) : ICommand<Guid>;

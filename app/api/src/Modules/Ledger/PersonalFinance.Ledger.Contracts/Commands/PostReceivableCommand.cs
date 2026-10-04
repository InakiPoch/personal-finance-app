using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Ledger.Contracts.Commands;

/// <summary>
/// Posts a shared expense the holder fronted in full.
/// </summary>
public sealed record PostReceivableCommand(Guid ExpenseAccountId, Guid ReceivableAccountId, Guid FundingAccountId, Money OwnShare, Money ReceivableShare,
    DateTimeOffset PostedOnUtc, Guid? SplitReferenceId = null) : ICommand<Guid>;

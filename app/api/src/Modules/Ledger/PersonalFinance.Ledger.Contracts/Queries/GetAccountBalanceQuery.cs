using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Ledger.Contracts.Queries;

/// <summary>
/// Returns the current balance of one account, signed by its normal balance side —
/// one row per currency that has entries on the account (accounts are poly-currency).
/// </summary>
public sealed record GetAccountBalanceQuery(Guid AccountId) : IQuery<IReadOnlyList<Money>>;

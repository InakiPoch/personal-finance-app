using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Ledger.Contracts.Queries;

/// <summary>
/// Returns the accrued card liability owned by the ledger (D11) — the posted <c>CardLiability</c>
/// balance only, never the un-accrued future schedule. Optionally scoped to one card account.
/// </summary>
public sealed record GetCardLiabilityQuery(Guid? CardAccountId = null) : IQuery<Money>;

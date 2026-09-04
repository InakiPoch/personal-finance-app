using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Ledger.Contracts.Queries;

/// <summary>
/// Resolves each installment reference id to the ledger transaction that accrued it — the original
/// post (never a reversal), so callers can offer a Reverse action against a statement row.
/// Installment references with no matching accrual are simply absent from the map.
/// </summary>
public sealed record AccrualTransactionIdsResponse(IReadOnlyDictionary<Guid, Guid> ByInstallmentReferenceId);

public sealed record FindAccrualTransactionIdsQuery(IReadOnlyList<Guid> InstallmentReferenceIds) : IQuery<AccrualTransactionIdsResponse>;

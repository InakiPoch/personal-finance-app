using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Financing.Contracts.Queries;

public sealed record PendingSharesByPartyRow(Guid PartyId, int ScheduledCount, long ScheduledTotalMinorUnits, string CurrencyCode);

/// <summary>
/// Per party, the count and summed minor-units of its not-yet-accrued installment shares across every
/// card-split and creditor-financed split plan — the same future-shares population exposed one party at a
/// time by <see cref="GetFuturePartySharesQuery"/>. Only parties with at least one pending share appear.
/// </summary>
public sealed record GetPendingSharesByPartyResponse(IReadOnlyList<PendingSharesByPartyRow> Rows);

public sealed record GetPendingSharesByPartyQuery() : IQuery<GetPendingSharesByPartyResponse>;

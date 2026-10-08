using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Financing.Contracts.Queries;

public sealed record PendingSharesByPartyRow(Guid PartyId, int ScheduledCount, long ScheduledTotalMinorUnits, string CurrencyCode);

/// <summary>
/// Per party, the count and summed minor-units of its not-yet-accrued installment shares across every card-split and creditor-financed split plan
/// </summary>
public sealed record GetPendingSharesByPartyResponse(IReadOnlyList<PendingSharesByPartyRow> Rows);

public sealed record GetPendingSharesByPartyQuery(DateOnly? ThroughMonth = null) : IQuery<GetPendingSharesByPartyResponse>;

using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Financing.Contracts.Queries;

public sealed record FuturePartyShareRow(int CycleYear, int CycleMonth, long ShareMinorUnits, string CurrencyCode, string SourceLabel);

/// <summary>
/// A party's not-yet-accrued monthly installment shares for its card-split plans, ordered by billing cycle.
/// </summary>
public sealed record GetFuturePartySharesResponse(IReadOnlyList<FuturePartyShareRow> Rows);

public sealed record GetFuturePartySharesQuery(Guid PartyId) : IQuery<GetFuturePartySharesResponse>;

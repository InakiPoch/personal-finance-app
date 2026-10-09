using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Parties.Contracts.Queries;

public sealed record ScheduledInstallmentRow(int CycleYear, int CycleMonth, long ShareMinorUnits, string CurrencyCode, string SourceLabel, Guid PurchaseId);

/// <summary>
/// The installments of a party's credit purchases that are not posted yet and not cancelled (what I will owe the party), ordered by month.
/// </summary>
public sealed record GetPartyScheduledInstallmentsResponse(IReadOnlyList<ScheduledInstallmentRow> Rows);

public sealed record GetPartyScheduledInstallmentsQuery(Guid PartyId) : IQuery<GetPartyScheduledInstallmentsResponse>;

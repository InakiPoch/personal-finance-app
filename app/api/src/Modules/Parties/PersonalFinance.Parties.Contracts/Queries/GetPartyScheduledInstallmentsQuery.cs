using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Parties.Contracts.Queries;

public sealed record ScheduledInstallmentRow(Guid PartyId, int CycleYear, int CycleMonth, long ShareMinorUnits, string CurrencyCode, string SourceLabel, Guid PurchaseId);

/// <summary>
/// The installments of credit purchases that are not posted yet and not cancelled.
/// </summary>
public sealed record GetPartyScheduledInstallmentsResponse(IReadOnlyList<ScheduledInstallmentRow> Rows);

public sealed record GetPartyScheduledInstallmentsQuery(Guid? PartyId = null) : IQuery<GetPartyScheduledInstallmentsResponse>;

using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Financing.Contracts.Queries;

/// <summary>
/// One payable source (card or creditor) with what is due this month in one currency.
/// </summary>
public sealed record DueThisMonthRow(string Kind, Guid SourceId, string SourceName, string CurrencyCode, long AmountMinorUnits);

public sealed record DueThisMonthResponse(IReadOnlyList<DueThisMonthRow> Rows);

public sealed record GetDueThisMonthQuery() : IQuery<DueThisMonthResponse>;

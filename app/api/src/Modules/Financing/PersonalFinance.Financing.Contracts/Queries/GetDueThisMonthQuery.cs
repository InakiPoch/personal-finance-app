using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Financing.Contracts.Queries;

/// <summary>
/// One payable source (card or creditor) with what is due this month in one currency.
/// </summary>
public sealed record DueThisMonthRow(string Kind, Guid SourceId, string SourceName, string CurrencyCode, long AmountMinorUnits);

public sealed record DueThisMonthResponse(IReadOnlyList<DueThisMonthRow> Rows);

/// <summary>
/// <paramref name="Month"/> is any date inside the requested calendar month; null means the current month.
/// <paramref name="Today"/> is the caller's local date; null falls back to the UTC date.
/// </summary>
public sealed record GetDueThisMonthQuery(DateOnly? Month = null, DateOnly? Today = null) : IQuery<DueThisMonthResponse>;

using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Financing.Contracts.Queries;

public sealed record CardClosingScheduleRow(int Year, int Month, DateOnly ClosingDate, bool IsOverride, bool IsLocked);

/// <summary>
/// Closing dates from the card's current open month forward. Found is false when the card does not exist.
/// </summary>
public sealed record CardClosingScheduleResponse(bool Found, Guid CardId, IReadOnlyList<CardClosingScheduleRow> Rows);

public sealed record GetCardClosingScheduleQuery(Guid CardId, int Months = 6) : IQuery<CardClosingScheduleResponse>;

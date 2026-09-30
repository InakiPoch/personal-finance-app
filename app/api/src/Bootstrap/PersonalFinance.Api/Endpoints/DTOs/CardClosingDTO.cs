namespace PersonalFinance.Api.Endpoints.DTOs;

public sealed record PutClosingDayDto(int Day);

public sealed record CardClosingDateDto(int Year, int Month, DateOnly ClosingDate, bool IsOverride, bool IsLocked);

public sealed record CardClosingDatesDto(IReadOnlyList<CardClosingDateDto> Rows);

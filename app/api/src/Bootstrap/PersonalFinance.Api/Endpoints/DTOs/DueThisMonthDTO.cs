namespace PersonalFinance.Api.Endpoints.DTOs;

public sealed record DueThisMonthRowDto(string Kind, Guid SourceId, string SourceName, string CurrencyCode, long AmountMinorUnits);

public sealed record DueThisMonthDto(IReadOnlyList<DueThisMonthRowDto> Rows);

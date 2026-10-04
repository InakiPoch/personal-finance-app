namespace PersonalFinance.Api.Endpoints.DTOs;

public sealed record InstrumentRowDto(Guid Id, string Type, string Name, int? CutoffDate, DateOnly? NextClosingDate);

public sealed record InstrumentsListDto(IReadOnlyList<InstrumentRowDto> Rows);

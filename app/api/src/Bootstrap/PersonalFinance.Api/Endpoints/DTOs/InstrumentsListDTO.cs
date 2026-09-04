namespace PersonalFinance.Api.Endpoints.DTOs;

public sealed record InstrumentRowDto(Guid Id, string Type, string Name, int? CutoffDate);

public sealed record InstrumentsListDto(IReadOnlyList<InstrumentRowDto> Rows);

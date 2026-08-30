namespace PersonalFinance.Api.Endpoints.DTOs;

public sealed record PostInstrumentDto(string Type, string Name, int? CutoffDate);

public sealed record InstrumentCreatedDto(Guid Id, string Type);

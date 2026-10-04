namespace PersonalFinance.Api.Endpoints.DTOs;

public sealed record PartyRowDto(Guid Id, string Name);

public sealed record PartiesListDto(IReadOnlyList<PartyRowDto> Rows);

namespace PersonalFinance.Api.Endpoints.DTOs;

public sealed record CreatePartyDto(string Name);

public sealed record PartyResultDto(Guid Id);

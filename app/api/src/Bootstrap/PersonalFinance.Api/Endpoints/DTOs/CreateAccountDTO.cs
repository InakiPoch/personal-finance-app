namespace PersonalFinance.Api.Endpoints.DTOs;

public sealed record CreateAccountDto(string Name, string Type, string Kind);

public sealed record CreateAccountResultDto(Guid AccountId);

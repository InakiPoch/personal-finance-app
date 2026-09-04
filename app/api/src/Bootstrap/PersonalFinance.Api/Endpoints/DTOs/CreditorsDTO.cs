namespace PersonalFinance.Api.Endpoints.DTOs;

public sealed record CreateCreditorAccountDto(string Label, string Identifier);

public sealed record CreateCreditorDto(string Name, IReadOnlyList<CreateCreditorAccountDto> Accounts);

public sealed record CreditorResultDto(Guid CreditorId);

public sealed record CreditorAccountRowDto(Guid Id, string Label, string Identifier);

public sealed record CreditorRowDto(Guid Id, string Name, IReadOnlyList<CreditorAccountRowDto> Accounts);

public sealed record CreditorListDto(IReadOnlyList<CreditorRowDto> Rows);

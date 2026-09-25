namespace PersonalFinance.Api.Endpoints.DTOs;

public sealed record AccountBalanceRowDto(long BalanceMinorUnits, string CurrencyCode, string Formatted);

public sealed record AccountBalanceDto(Guid AccountId, IReadOnlyList<AccountBalanceRowDto> Rows);

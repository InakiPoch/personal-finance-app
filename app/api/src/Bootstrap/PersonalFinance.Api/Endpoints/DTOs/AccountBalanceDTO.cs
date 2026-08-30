namespace PersonalFinance.Api.Endpoints.DTOs;

public sealed record AccountBalanceDto(Guid AccountId, long BalanceMinorUnits, string CurrencyCode, string Formatted);

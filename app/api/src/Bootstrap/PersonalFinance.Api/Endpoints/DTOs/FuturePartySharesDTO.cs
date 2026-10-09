namespace PersonalFinance.Api.Endpoints.DTOs;

public sealed record FuturePartyShareDto(int CycleYear, int CycleMonth, long ShareMinorUnits, string CurrencyCode, string SourceLabel, Guid? PurchaseId = null);

public sealed record FuturePartySharesDto(IReadOnlyList<FuturePartyShareDto> Rows);

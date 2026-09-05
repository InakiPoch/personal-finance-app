namespace PersonalFinance.Api.Endpoints.DTOs;

public sealed record CardPurchaseRowDto(Guid PlanId, string Description, long TotalMinorUnits, int InstallmentCount, int OutstandingCount, DateOnly PurchaseDate);

public sealed record CardPurchasesDto(Guid CardId, IReadOnlyList<CardPurchaseRowDto> Rows);

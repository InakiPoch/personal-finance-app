namespace PersonalFinance.Api.Endpoints.DTOs;

public sealed record RecentPurchaseRowDto(Guid PlanId, string Description, string CardName, DateOnly PurchaseDate, long TotalMinorUnits, int InstallmentCount, bool IsCreditorPayment, int PaidInstallmentCount, int? NextDueYear, int? NextDueMonth);

public sealed record RecentPurchasesDto(IReadOnlyList<RecentPurchaseRowDto> Rows);

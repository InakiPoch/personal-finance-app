namespace PersonalFinance.Api.Endpoints.DTOs;

public sealed record CreditorInstallmentRowDto(Guid InstallmentId, int Sequence, int InstallmentCount, long AmountMinorUnits, int DueYear, int DueMonth, bool IsPaid, bool IsReversed, string Status);

public sealed record CreditorPurchaseGroupDto(Guid PlanId, string Description, DateOnly PurchaseDate, long TotalMinorUnits, long OutstandingMinorUnits, IReadOnlyList<CreditorInstallmentRowDto> Installments);

public sealed record CreditorDetailDto(Guid CreditorId, string CreditorName, IReadOnlyList<CreditorPurchaseGroupDto> Purchases);

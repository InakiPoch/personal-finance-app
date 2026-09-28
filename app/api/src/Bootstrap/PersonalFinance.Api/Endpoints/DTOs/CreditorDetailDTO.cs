namespace PersonalFinance.Api.Endpoints.DTOs;

public sealed record CreditorInstallmentPartyShareDto(Guid PartyId, string PartyName, long ShareMinorUnits, bool IsPaid);

public sealed record CreditorInstallmentRowDto(Guid InstallmentId, int Sequence, int InstallmentCount, long AmountMinorUnits, int DueYear, int DueMonth, bool IsPaid, bool IsReversed, string Status, long PaidMinorUnits, long RemainingMinorUnits, bool HasPayments, IReadOnlyList<CreditorInstallmentPartyShareDto> PartyShares);

public sealed record CreditorPurchaseGroupDto(Guid PlanId, string Description, DateOnly PurchaseDate, long TotalMinorUnits, long OutstandingMinorUnits, IReadOnlyList<CreditorInstallmentRowDto> Installments, string CurrencyCode);

public sealed record CreditorDetailDto(Guid CreditorId, string CreditorName, IReadOnlyList<CreditorPurchaseGroupDto> Purchases);

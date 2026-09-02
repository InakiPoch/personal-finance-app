namespace PersonalFinance.Api.Endpoints.DTOs;

public sealed record MonthlyStatementInstallmentRowDto(Guid PlanId, Guid InstallmentId, int Sequence, int InstallmentCount, string PurchaseDate, int CycleYear, int CycleMonth, long AmountMinorUnits, bool IsReversed);

public sealed record MonthlyStatementDetailDto(Guid StatementId, Guid CardId, string CardName, int CycleYear, int CycleMonth, long AmountDueMinorUnits, bool IsPaid, DateTimeOffset? PaidOnUtc, IReadOnlyList<MonthlyStatementInstallmentRowDto> Installments);

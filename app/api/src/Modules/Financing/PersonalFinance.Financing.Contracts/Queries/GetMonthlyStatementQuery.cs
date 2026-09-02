using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Financing.Contracts.Queries;

public sealed record MonthlyStatementInstallmentRow( Guid PlanId, Guid InstallmentId, int Sequence, int InstallmentCount, DateOnly PurchaseDate, int CycleYear, int CycleMonth, long AmountMinorUnits, bool IsReversed);

public sealed record MonthlyStatementDetailResponse( bool Found, Guid StatementId, Guid CardId, string CardName, int CycleYear, int CycleMonth, long AmountDueMinorUnits, bool IsPaid, DateTimeOffset? PaidOnUtc, IReadOnlyList<MonthlyStatementInstallmentRow> Installments);

public sealed record GetMonthlyStatementQuery(Guid StatementId) : IQuery<MonthlyStatementDetailResponse>;

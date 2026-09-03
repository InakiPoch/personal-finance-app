namespace PersonalFinance.Api.Endpoints.DTOs;

public sealed record CardStatementRowDto(Guid StatementId, Guid CardId, string CardName, int CycleYear, int CycleMonth, long AmountDueMinorUnits, bool IsPaid, DateTimeOffset? PaidOnUtc);

public sealed record CardStatementsDto(Guid CardId, IReadOnlyList<CardStatementRowDto> Rows);

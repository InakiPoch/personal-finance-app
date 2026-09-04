namespace PersonalFinance.Api.Endpoints.DTOs;

public sealed record MonthlyExpenseRowDto(string Month, string Category, long AmountMinorUnits, string CurrencyCode);

public sealed record MonthlyExpensesDto(IReadOnlyList<MonthlyExpenseRowDto> Rows);

public sealed record CardDueRowDto(
    string Bucket,
    string Card,
    int? CycleYear,
    int? CycleMonth,
    long AmountMinorUnits,
    string CurrencyCode,
    string? CardId
);

public sealed record CardDueByMonthDto(IReadOnlyList<CardDueRowDto> Rows);

public sealed record PartyTimelineRowDto(
    Guid TransactionId,
    DateTimeOffset MovementOnUtc,
    string Description,
    long DeltaMinorUnits,
    long RunningBalanceMinorUnits,
    string CurrencyCode
);

public sealed record PartyTimelineDto(IReadOnlyList<PartyTimelineRowDto> Rows);

public sealed record PartyDebtRowDto(Guid PartyId, string PartyName, long NetBalanceMinorUnits, string CurrencyCode);

public sealed record DebtByPartyDto(IReadOnlyList<PartyDebtRowDto> Rows);

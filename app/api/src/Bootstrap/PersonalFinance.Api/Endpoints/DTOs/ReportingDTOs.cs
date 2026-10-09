namespace PersonalFinance.Api.Endpoints.DTOs;

public sealed record MonthlyExpenseRowDto(string Month, string Category, long AmountMinorUnits, string CurrencyCode);

public sealed record MonthlyExpensesDto(IReadOnlyList<MonthlyExpenseRowDto> Rows);

public sealed record MonthlyIncomeRowDto(string Month, long AmountMinorUnits, string CurrencyCode);

public sealed record MonthlyIncomesDto(IReadOnlyList<MonthlyIncomeRowDto> Rows);

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
    string CurrencyCode,
    Guid? PurchaseId
);

public sealed record PartyTimelineDto(IReadOnlyList<PartyTimelineRowDto> Rows);

public sealed record PartyDebtRowDto(Guid PartyId, string PartyName, long NetBalanceMinorUnits, string CurrencyCode);

public sealed record DebtByPartyDto(IReadOnlyList<PartyDebtRowDto> Rows);

public sealed record MoneyFlowRowDto(
    Guid TransactionId,
    DateOnly Date,
    string Description,
    string AccountName,
    string Kind,
    long AmountMinorUnits,
    string CurrencyCode,
    string? Flag,
    string? PartyName
);

public sealed record MoneyFlowDto(IReadOnlyList<MoneyFlowRowDto> Rows);

public sealed record OwedToYouRowDto(Guid PartyId, string PartyName, string CurrencyCode, long AmountMinorUnits);

public sealed record OwedToYouDto(IReadOnlyList<OwedToYouRowDto> Rows);

public sealed record YouOweRowDto(Guid PartyId, string PartyName, string CurrencyCode, long AmountMinorUnits);

public sealed record YouOweDto(IReadOnlyList<YouOweRowDto> Rows);

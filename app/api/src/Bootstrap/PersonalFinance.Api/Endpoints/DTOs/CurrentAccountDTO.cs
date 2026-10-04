namespace PersonalFinance.Api.Endpoints.DTOs;

public sealed record PartyCurrencyBalanceDto(string CurrencyCode, long BalanceMinorUnits);

public sealed record CurrentAccountBalanceDto(Guid PartyId, string Name, IReadOnlyList<PartyCurrencyBalanceDto> Balances);

public sealed record CurrentAccountTimelineRowDto(
    Guid TransactionId,
    DateTimeOffset MovementOnUtc,
    string Description,
    long DeltaMinorUnits,
    long RunningBalanceMinorUnits,
    string CurrencyCode
);

public sealed record CurrentAccountTimelineDto(IReadOnlyList<CurrentAccountTimelineRowDto> Rows);

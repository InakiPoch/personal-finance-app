namespace PersonalFinance.Api.Endpoints.DTOs;

public sealed record CurrentAccountBalanceDto(Guid PartyId, string Name, long BalanceMinorUnits);

public sealed record CurrentAccountTimelineRowDto(
    Guid TransactionId,
    DateTimeOffset MovementOnUtc,
    string Description,
    long DeltaMinorUnits,
    long RunningBalanceMinorUnits
);

public sealed record CurrentAccountTimelineDto(IReadOnlyList<CurrentAccountTimelineRowDto> Rows);

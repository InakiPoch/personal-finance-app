namespace PersonalFinance.Api.Endpoints.DTOs;

/// <summary>
/// One party with both sides per currency, never netted.
/// </summary>
public sealed record PartyRowDto(
    Guid Id,
    string Name,
    IReadOnlyList<PartyCurrencyBalanceDto> OwedToYou,
    IReadOnlyList<PartyCurrencyBalanceDto> YouOwe,
    int ScheduledToYouCount,
    int ScheduledYouOweCount,
    bool SettledUp
);

public sealed record PartiesListDto(IReadOnlyList<PartyRowDto> Rows);

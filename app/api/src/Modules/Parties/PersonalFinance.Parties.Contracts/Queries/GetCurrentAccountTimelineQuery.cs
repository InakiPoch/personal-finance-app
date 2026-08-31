using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Parties.Contracts.Queries;

/// <summary>
/// One movement on a party's current account, with the running balance after it.
/// <paramref name="DeltaMinorUnits"/> is positive when the party's debt grows, negative when it
/// is settled or a split is reversed.
/// </summary>
public sealed record CurrentAccountTimelineRow(
    DateTimeOffset MovementOnUtc,
    string Description,
    long DeltaMinorUnits,
    long RunningBalanceMinorUnits
);

public sealed record CurrentAccountTimelineResponse(IReadOnlyList<CurrentAccountTimelineRow> Rows);

public sealed record GetCurrentAccountTimelineQuery(Guid PartyId) : IQuery<CurrentAccountTimelineResponse>;

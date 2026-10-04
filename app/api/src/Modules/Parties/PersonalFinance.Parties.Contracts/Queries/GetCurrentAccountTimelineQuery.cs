using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Parties.Contracts.Queries;

/// <summary>
/// One movement on a party's current account, with the running balance after it.
/// <paramref name="DeltaMinorUnits"/> is positive when the party's debt grows, negative when it
/// is settled or a split is reversed. <paramref name="TransactionId"/> is the ledger transaction
/// behind the movement, so a caller can offer a Reverse action against the row.
/// </summary>
public sealed record CurrentAccountTimelineRow(
    Guid TransactionId,
    DateTimeOffset MovementOnUtc,
    string Description,
    long DeltaMinorUnits,
    long RunningBalanceMinorUnits,
    string CurrencyCode
);

public sealed record CurrentAccountTimelineResponse(IReadOnlyList<CurrentAccountTimelineRow> Rows);

public sealed record GetCurrentAccountTimelineQuery(Guid PartyId) : IQuery<CurrentAccountTimelineResponse>;

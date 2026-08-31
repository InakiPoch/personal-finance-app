namespace PersonalFinance.Parties.Infrastructure.Persistence.ReadModels;

/// <summary>
/// One row of <c>vw_current_account_timeline</c> — a single movement on a party's Ledger
/// receivable account, with the running balance after it.
/// </summary>
internal sealed class CurrentAccountTimelineEntry {
    public Guid PartyId { get; init; }
    public string PartyName { get; init; } = string.Empty;
    public Guid AccountId { get; init; }
    public DateTimeOffset MovementOnUtc { get; init; }
    public string Description { get; init; } = string.Empty;
    public long DeltaMinorUnits { get; init; }
    public long RunningBalanceMinorUnits { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
}

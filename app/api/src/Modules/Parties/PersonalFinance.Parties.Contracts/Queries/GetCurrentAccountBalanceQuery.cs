using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Parties.Contracts.Queries;

/// <summary>
/// One currency of a party's outstanding balance.
/// </summary>
public sealed record PartyCurrencyBalance(string CurrencyCode, long BalanceMinorUnits);

/// <summary>
/// A party's outstanding balance, one row per currency it holds. Always resolved live from the
/// party's Ledger <c>Receivable</c> account. Positive means the party owes the account holder. PayableBalances is the separate, never-netted
/// <c>PartyPayable</c> side: positive means the account holder owes the party.
/// A currency with no movements is absent, never a zero row (no FX, no blending).
/// </summary>
public sealed record CurrentAccountBalanceResponse(Guid PartyId, string Name, IReadOnlyList<PartyCurrencyBalance> Balances, IReadOnlyList<PartyCurrencyBalance> PayableBalances);

public sealed record GetCurrentAccountBalanceQuery(Guid PartyId) : IQuery<CurrentAccountBalanceResponse>;

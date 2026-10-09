using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Parties.Contracts.Queries;

/// <summary>
/// One currency of a party's outstanding balance.
/// </summary>
public sealed record PartyCurrencyBalance(string CurrencyCode, long BalanceMinorUnits);

/// <summary>
/// A party's outstanding balance, one row per currency.
/// </summary>
public sealed record CurrentAccountBalanceResponse(Guid PartyId, string Name, IReadOnlyList<PartyCurrencyBalance> Balances, IReadOnlyList<PartyCurrencyBalance> PayableBalances);

public sealed record GetCurrentAccountBalanceQuery(Guid PartyId) : IQuery<CurrentAccountBalanceResponse>;

using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Parties.Contracts.Queries;

/// <summary>
/// A party's outstanding balance. Always resolved live from the party's Ledger <c>Receivable</c>. Positive means the party owes the account holder.
/// </summary>
public sealed record CurrentAccountBalanceResponse(Guid PartyId, string Name, long BalanceMinorUnits);

public sealed record GetCurrentAccountBalanceQuery(Guid PartyId) : IQuery<CurrentAccountBalanceResponse>;

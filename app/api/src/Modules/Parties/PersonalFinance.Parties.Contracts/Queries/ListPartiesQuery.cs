using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Parties.Contracts.Queries;

public sealed record PartyRow(Guid Id, string Name);

/// <summary>
/// Every registered party, ordered by name. No ledger join — parties with zero movements are included.
/// </summary>
public sealed record ListPartiesResponse(IReadOnlyList<PartyRow> Rows);

public sealed record ListPartiesQuery() : IQuery<ListPartiesResponse>;

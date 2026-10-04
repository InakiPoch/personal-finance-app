using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Ledger.Contracts.Queries;

public sealed record InstrumentAccountRow(Guid AccountId, string Name, string Kind);

/// <summary>
/// The Ledger accounts that back registered debit/cash instruments, ordered by name.
/// </summary>
public sealed record InstrumentAccountsResponse(IReadOnlyList<InstrumentAccountRow> Rows);

public sealed record ListInstrumentAccountsQuery() : IQuery<InstrumentAccountsResponse>;

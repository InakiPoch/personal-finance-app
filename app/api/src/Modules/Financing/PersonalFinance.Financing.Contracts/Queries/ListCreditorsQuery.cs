using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Financing.Contracts.Queries;

public sealed record CreditorAccountRow(Guid Id, string Label, string? Identifier);

public sealed record CreditorRow(Guid Id, string Name, IReadOnlyList<CreditorAccountRow> Accounts);

/// <summary>
/// Every registered creditor with its accounts, ordered by name.
/// </summary>
public sealed record ListCreditorsResponse(IReadOnlyList<CreditorRow> Rows);

public sealed record ListCreditorsQuery() : IQuery<ListCreditorsResponse>;

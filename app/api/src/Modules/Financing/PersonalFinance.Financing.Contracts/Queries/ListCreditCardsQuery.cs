using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Financing.Contracts.Queries;

public sealed record CreditCardRow(Guid CardId, string Name, int CutoffDay);

/// <summary>
/// Every registered credit card, ordered by name.
/// </summary>
public sealed record ListCreditCardsResponse(IReadOnlyList<CreditCardRow> Rows);

public sealed record ListCreditCardsQuery() : IQuery<ListCreditCardsResponse>;

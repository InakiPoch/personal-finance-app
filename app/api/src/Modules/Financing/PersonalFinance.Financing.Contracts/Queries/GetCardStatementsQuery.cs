using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Financing.Contracts.Queries;

public sealed record CardStatementRow( Guid StatementId, Guid CardId, string CardName, int CycleYear, int CycleMonth, long AmountDueMinorUnits, bool IsPaid, DateTimeOffset? PaidOnUtc);

/// <summary>
/// Every monthly statement raised for a card, ordered by billing cycle.
/// </summary>
public sealed record CardStatementsResponse(IReadOnlyList<CardStatementRow> Rows);

public sealed record GetCardStatementsQuery(Guid CardId) : IQuery<CardStatementsResponse>;

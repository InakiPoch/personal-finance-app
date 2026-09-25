using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Ledger.Contracts.Queries;

public sealed record TransactionFeedRow(
    Guid TransactionId,
    DateTimeOffset PostedOnUtc,
    string Description,
    long AmountMinorUnits,
    string CurrencyCode,
    bool IsReversal,
    bool IsReversed,
    Guid? InstallmentReferenceId,
    Guid? SplitReferenceId);

/// <summary>
/// The Ledger transaction feed, newest first, optionally narrowed to one account and a posted-date range.
/// </summary>
public sealed record TransactionFeedResponse(IReadOnlyList<TransactionFeedRow> Rows);

public sealed record GetTransactionsQuery(Guid? AccountId, DateOnly? FromUtc, DateOnly? ToUtc) : IQuery<TransactionFeedResponse>;

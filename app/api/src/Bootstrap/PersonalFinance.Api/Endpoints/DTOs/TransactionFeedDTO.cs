namespace PersonalFinance.Api.Endpoints.DTOs;

public sealed record TransactionFeedRowDto(
    Guid TransactionId,
    DateTimeOffset PostedOnUtc,
    string Description,
    long AmountMinorUnits,
    string CurrencyCode,
    bool IsReversal,
    bool IsReversed,
    Guid? InstallmentReferenceId,
    Guid? SplitReferenceId
);

public sealed record TransactionFeedDto(IReadOnlyList<TransactionFeedRowDto> Rows);

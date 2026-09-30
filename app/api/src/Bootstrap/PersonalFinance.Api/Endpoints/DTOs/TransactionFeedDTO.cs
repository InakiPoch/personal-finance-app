namespace PersonalFinance.Api.Endpoints.DTOs;

public sealed record TransactionFeedRowDto(
    Guid Id,
    DateTimeOffset PostedOnUtc,
    string Kind,
    string Description,
    IReadOnlyList<string> FromAccounts,
    IReadOnlyList<string> ToAccounts,
    long AmountMinorUnits,
    string CurrencyCode,
    bool IsUndoEntry,
    bool IsUndone,
    IReadOnlyList<string> ImpactLines
);

public sealed record TransactionFeedDto(IReadOnlyList<TransactionFeedRowDto> Rows);

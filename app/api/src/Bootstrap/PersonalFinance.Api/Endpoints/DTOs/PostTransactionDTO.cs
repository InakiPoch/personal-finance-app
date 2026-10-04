namespace PersonalFinance.Api.Endpoints.DTOs;

/// <summary>
/// One part of a <see cref="PostTransactionDto"/>. <c>Direction</c> is "Debit" or "Credit".
/// </summary>
public sealed record PostTransactionLineDto(Guid AccountId, string Direction, long AmountMinorUnits);

public sealed record PostTransactionDto(
    IReadOnlyList<PostTransactionLineDto> Lines,
    DateTimeOffset PostedOnUtc,
    Guid? SplitReferenceId = null,
    Guid? InstallmentReferenceId = null,
    string? Description = null
);

public sealed record PostTransactionResultDto(Guid TransactionId);

using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Financing.Contracts.Queries;

public sealed record CreditorPayableAccountBreakdown(Guid AccountId, string Label, long OutstandingMinorUnits);

/// <summary>
/// One creditor's outstanding creditor-financed balance, its next owed date, and the per-account split.
/// </summary>
public sealed record CreditorPayableRow(
    Guid CreditorId,
    string CreditorName,
    long OutstandingMinorUnits,
    DateOnly? NextDueDate,
    IReadOnlyList<CreditorPayableAccountBreakdown> Accounts
);

public sealed record CreditorPayablesResponse(IReadOnlyList<CreditorPayableRow> Rows);

public sealed record GetCreditorPayablesQuery() : IQuery<CreditorPayablesResponse>;

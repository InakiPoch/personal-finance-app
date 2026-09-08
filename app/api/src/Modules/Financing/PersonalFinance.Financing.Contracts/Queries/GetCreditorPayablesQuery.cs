using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Financing.Contracts.Queries;

public sealed record CreditorPayableAccountBreakdown(Guid AccountId, string Label, long OutstandingMinorUnits);

/// <summary>
/// One creditor's outstanding creditor-financed balance, split into the amount due by the current
/// billing cycle (arrears folded in) and the whole remaining debt, plus its next owed date and the
/// per-account breakdown. Paid installments are excluded from both figures.
/// </summary>
public sealed record CreditorPayableRow(
    Guid CreditorId,
    string CreditorName,
    long DueNowMinorUnits,
    long TotalOwedMinorUnits,
    DateOnly? NextDueDate,
    IReadOnlyList<CreditorPayableAccountBreakdown> Accounts
);

public sealed record CreditorPayablesResponse(IReadOnlyList<CreditorPayableRow> Rows);

public sealed record GetCreditorPayablesQuery() : IQuery<CreditorPayablesResponse>;

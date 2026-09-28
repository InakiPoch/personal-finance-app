using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Financing.Contracts.Queries;

/// <summary>
/// One split participant's share of a single installment.
/// </summary>
public sealed record CreditorInstallmentPartyShare(Guid PartyId, string PartyName, long ShareMinorUnits, bool IsPaid);

public sealed record CreditorInstallmentRow(
    Guid InstallmentId,
    int Sequence,
    int InstallmentCount,
    long AmountMinorUnits,
    int DueYear,
    int DueMonth,
    bool IsPaid,
    bool IsReversed,
    string Status,
    long PaidMinorUnits,
    long RemainingMinorUnits,
    bool HasPayments,
    IReadOnlyList<CreditorInstallmentPartyShare> PartyShares
);

public sealed record CreditorPurchaseGroup(
    Guid PlanId,
    string Description,
    DateOnly PurchaseDate,
    long TotalMinorUnits,
    long OutstandingMinorUnits,
    IReadOnlyList<CreditorInstallmentRow> Installments,
    string CurrencyCode
);

/// <summary>
/// One creditor's debt broken down by purchase: every creditor-financed payment plan a section, its
/// installments listed beneath with sequence, amount, due month and paid/reversed status. Groups are
/// ordered newest purchase first.
/// </summary>
public sealed record CreditorDetailResponse(
    bool Found,
    Guid CreditorId,
    string CreditorName,
    IReadOnlyList<CreditorPurchaseGroup> Purchases
);

public sealed record GetCreditorDetailQuery(Guid CreditorId) : IQuery<CreditorDetailResponse>;

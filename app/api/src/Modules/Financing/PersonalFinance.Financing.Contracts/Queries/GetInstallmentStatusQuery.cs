using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Financing.Contracts.Queries;

public sealed record InstallmentStatusResponse(
    bool Exists,
    bool Accrued,
    bool Paid,
    Guid? StatementId,
    long AmountMinorUnits,
    bool Reversed,
    Guid CardId,
    Guid CardCreditAccountId,
    Guid CardLiabilityAccountId
);

public sealed record GetInstallmentStatusQuery(Guid InstallmentId) : IQuery<InstallmentStatusResponse>;

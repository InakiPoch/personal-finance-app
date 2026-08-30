using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Financing.Contracts.Queries;

public sealed record InstallmentStatusResponse(bool Exists, bool Accrued, bool Paid, Guid? StatementId, long AmountMinorUnits);

/// <summary>
/// Returns the <see cref="InstallmentStatusResponse"/> for one installment.
/// </summary>
public sealed record GetInstallmentStatusQuery(Guid InstallmentId) : IQuery<InstallmentStatusResponse>;

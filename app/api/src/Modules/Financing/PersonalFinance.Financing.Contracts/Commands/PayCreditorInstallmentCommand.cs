using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Financing.Contracts.Commands;

/// <summary>
/// Records a payment against one creditor-financed installment — a display-only ledger-free row, full
/// or partial. <see cref="AmountMinorUnits"/> null means "pay whatever remains" (a full payment); once
/// the sum of payments reaches the installment's amount, <c>Installment.PaidOnUtc</c> is stamped with
/// the server clock at handling time.
/// </summary>
public sealed record PayCreditorInstallmentCommand(Guid InstallmentId, long? AmountMinorUnits = null) : ICommand<Guid>;

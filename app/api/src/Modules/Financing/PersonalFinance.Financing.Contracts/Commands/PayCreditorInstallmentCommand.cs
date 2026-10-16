using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Financing.Contracts.Commands;

/// <summary>
/// Records a payment against one creditor-financed installment, full or partial, paid from the
/// <see cref="BankAccountId"/> Bank/Cash account (ADR 0018). <see cref="AmountMinorUnits"/> null means "pay whatever remains" (a full payment); once
/// the sum of payments reaches the installment's amount, <c>Installment.PaidOnUtc</c> is stamped with
/// the server clock at handling time.
/// </summary>
public sealed record PayCreditorInstallmentCommand(Guid InstallmentId, Guid BankAccountId, long? AmountMinorUnits = null) : ICommand<Guid>;

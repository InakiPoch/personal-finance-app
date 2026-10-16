using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Financing.Contracts.Commands;

/// <summary>
/// Pays one creditor installment from a Bank/Cash account; a null amount pays what remains.
/// </summary>
public sealed record PayCreditorInstallmentCommand(Guid InstallmentId, Guid BankAccountId, long? AmountMinorUnits = null) : ICommand<Guid>;

using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Financing.Contracts.Commands;

/// <summary>
/// Pays a single accrued credit-card installment in full from a bank account.
/// </summary>
public sealed record PayInstallmentCommand(Guid InstallmentId, Guid BankAccountId, DateTimeOffset PaidOnUtc) : ICommand<Guid>;

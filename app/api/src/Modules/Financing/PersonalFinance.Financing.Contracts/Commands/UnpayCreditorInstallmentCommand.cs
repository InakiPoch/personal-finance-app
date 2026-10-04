using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Financing.Contracts.Commands;

/// <summary>
/// Reverts a creditor installment's display-only paid stamp (fat-finger recovery). There is no ledger
/// transaction to storno — it just clears <c>PaidOnUtc</c>.
/// </summary>
public sealed record UnpayCreditorInstallmentCommand(Guid InstallmentId) : ICommand<Guid>;

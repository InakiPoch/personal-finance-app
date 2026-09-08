using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Financing.Contracts.Commands;

/// <summary>
/// Marks one creditor-financed installment paid — a display-only <c>PaidOnUtc</c> stamp with no bank
/// account and no ledger posting (creditor debt is ledger-free for the holder). The timestamp is the
/// server clock at handling time.
/// </summary>
public sealed record PayCreditorInstallmentCommand(Guid InstallmentId) : ICommand<Guid>;

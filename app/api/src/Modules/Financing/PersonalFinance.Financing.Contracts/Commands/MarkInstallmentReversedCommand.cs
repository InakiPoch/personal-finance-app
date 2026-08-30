using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Financing.Contracts.Commands;

/// <summary>
/// Marks an installment reversed on the Financing side after the Ledger has posted the storno.
/// When <see cref="CompensatingCreditPosted"/> is set, the card carries forward
/// <see cref="CreditAmountMinorUnits"/> as a credit to net against its next statement.
/// </summary>
public sealed record MarkInstallmentReversedCommand(Guid InstallmentId, bool CompensatingCreditPosted, long CreditAmountMinorUnits) : ICommand;

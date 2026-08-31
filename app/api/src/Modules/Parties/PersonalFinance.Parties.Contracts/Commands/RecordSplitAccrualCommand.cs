using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Parties.Contracts.Commands;

/// <summary>
/// Advances a card-plan <c>ExpenseSplit</c>'s accrued-receivable total as Financing posts each installment's party legs to the Ledger.
/// </summary>
public sealed record RecordSplitAccrualCommand(Guid SplitReferenceId, long AccruedReceivableMinorUnits) : ICommand;

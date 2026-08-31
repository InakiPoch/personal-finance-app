using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Parties.Contracts.Commands;

/// <summary>
/// The synchronous Ledger-&gt;Parties reversal correction. Called by <c>ReverseTransactionHandler</c> when the reversed transaction carried a split reference.
/// </summary>
public sealed record CorrectExpenseSplitCommand(
    Guid SplitReferenceId,
    Guid? InstallmentReferenceId,
    long ReversedReceivableMinorUnits,
    DateTimeOffset CorrectedOnUtc
) : ICommand;

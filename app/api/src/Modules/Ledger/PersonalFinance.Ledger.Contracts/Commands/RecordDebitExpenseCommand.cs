using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Ledger.Contracts.Commands;

public sealed record RecordDebitExpenseParticipant(Guid PartyId, long Weight);

/// <summary>
/// Records a debit or cash expense as one balanced Ledger transaction: the money leaves
/// <see cref="SourceAccountId"/> (a Bank or Cash account) and lands on the expense category
/// named <see cref="CategoryName"/> (resolved get-or-create by name). When <see cref="Split"/>
/// is set, each participant's share posts to their receivable instead — the holder's share
/// stays on the category — exactly as the credit-card split does. Returns the Ledger
/// transaction id for an unsplit expense, or the expense-split id when split across parties.
/// </summary>
public sealed record RecordDebitExpenseCommand(
    long AmountMinorUnits,
    Guid SourceAccountId,
    string CategoryName,
    DateOnly PurchaseDate,
    string Description,
    IReadOnlyList<RecordDebitExpenseParticipant>? Split = null,
    string CurrencyCode = "ARS"
) : ICommand<Guid>;

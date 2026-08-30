namespace PersonalFinance.Ledger.Contracts;

/// <summary>
/// The side an <c>Entry</c> posts on. Every balanced <c>Transaction</c> has
/// Σ(<see cref="Debit"/> amounts) == Σ(<see cref="Credit"/> amounts) in a single currency.
/// </summary>
public enum DebitOrCredit {
    Debit,
    Credit
}

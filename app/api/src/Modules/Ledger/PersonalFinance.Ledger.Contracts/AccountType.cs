namespace PersonalFinance.Ledger.Contracts;

/// <summary>
/// The accounting classification of a ledger account. Fixes the normal balance side:
/// <see cref="Asset"/> and <see cref="Expense"/> are debit-positive; <see cref="Liability"/>,
/// <see cref="Income"/> and <see cref="Equity"/> are credit-positive.
/// </summary>
public enum AccountType {
    Asset,
    Liability,
    Expense,
    Income,
    Equity
}

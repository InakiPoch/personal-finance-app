namespace PersonalFinance.Ledger.Contracts;

/// <summary>
/// The functional role of a ledger account within the personal-finance domain.
/// </summary>
public enum AccountKind {
    Bank,
    Cash,
    Receivable,
    CardLiability,
    CardCredit,
    Expense,
    Income,
    Equity
}

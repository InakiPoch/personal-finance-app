using PersonalFinance.SharedKernel;

namespace PersonalFinance.Ledger.Domain;

/// <summary>
/// Domain error catalog for the Ledger context.
/// </summary>
internal static class LedgerErrors {
    public static readonly Error Unbalanced = new(
        "Ledger.Unbalanced",
        "The transaction does not balance: total debits must equal total credits."
    );

    public static readonly Error MixedCurrency = new(
        "Ledger.MixedCurrency",
        "All entries in a transaction must share a single currency."
    );

    public static readonly Error DegenerateTransaction = new(
        "Ledger.DegenerateTransaction",
        "A transaction must have at least two entries."
    );

    public static readonly Error CannotReverseAReversal = new(
        "Ledger.CannotReverseAReversal",
        "A reversal transaction cannot itself be reversed."
    );

    public static readonly Error OriginalTransactionNotFound = new(
        "Ledger.OriginalTransactionNotFound",
        "The transaction to reverse was not found."
    );

    public static readonly Error AccountNotFound = new(
        "Ledger.AccountNotFound",
        "The referenced account was not found."
    );

    public static readonly Error InvalidAccountName = new(
        "Ledger.InvalidAccountName",
        "An account name must not be blank."
    );

    public static readonly Error IncoherentAccountKind = new(
        "Ledger.IncoherentAccountKind",
        "The account kind is not valid for the given account type."
    );

    public static readonly Error NonPositiveEntryAmount = new(
        "Ledger.NonPositiveEntryAmount",
        "Every entry amount must be a positive number of minor units."
    );

    public static readonly Error InvalidExpenseCategory = new(
        "Ledger.InvalidExpenseCategory",
        "A debit or cash expense requires a category name."
    );

    public static readonly Error InvalidExpenseDescription = new(
        "Ledger.InvalidExpenseDescription",
        "A debit or cash expense requires a description."
    );

    public static readonly Error InvalidExpenseSplit = new(
        "Ledger.InvalidExpenseSplit",
        "An expense split needs at least one participant, each a distinct party with a positive weight."
    );

    public static readonly Error SourceAccountNotSpendable = new(
        "Ledger.SourceAccountNotSpendable",
        "The source of a debit or cash expense must be a debit or cash account."
    );
}

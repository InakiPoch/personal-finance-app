using PersonalFinance.Ledger.Contracts;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Ledger.Domain;

/// <summary>
/// A ledger account. Its balance is derived from posted entries.
/// </summary>
internal sealed class Account : AggregateRoot<Guid> {
    public string Name { get; }
    public AccountType Type { get; }
    public AccountKind Kind { get; }
    public Guid? OwnerReferenceId { get; private set; }

    private Account(Guid id, string name, AccountType type, AccountKind kind) : base(id) {
        Name = name;
        Type = type;
        Kind = kind;
    }

    public static Result<Account> Create(string name, AccountType type, AccountKind kind, Guid? ownerReferenceId = null) {
        if(string.IsNullOrWhiteSpace(name)) {
            return LedgerErrors.InvalidAccountName;
        }
        if(!kindMatchesType(kind, type)) {
            return LedgerErrors.IncoherentAccountKind;
        }
        return new Account(Guid.CreateVersion7(), name.Trim(), type, kind) { OwnerReferenceId = ownerReferenceId };
    }

    private static bool kindMatchesType(AccountKind kind, AccountType type) {
        return kind switch {
            AccountKind.Bank or AccountKind.Cash or AccountKind.Receivable or AccountKind.CardCredit => type == AccountType.Asset,
            AccountKind.CardLiability => type == AccountType.Liability,
            AccountKind.CardPurchases or AccountKind.Expense => type == AccountType.Expense,
            AccountKind.Income => type == AccountType.Income,
            AccountKind.Equity => type == AccountType.Equity,
            _ => false
        };
    }
}

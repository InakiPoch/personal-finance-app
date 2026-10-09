using PersonalFinance.SharedKernel;

namespace PersonalFinance.Parties.Domain;

/// <summary>
/// One slice of my share of a party purchase; it is posted to the ledger once its transaction id is set.
/// </summary>
internal sealed class PartyPurchaseInstallment : Entity<Guid> {
    public Guid PartyPurchaseId { get; }
    public int Number { get; }
    public DateOnly DueOn { get; }
    public Guid? LedgerTransactionId { get; private set; }

    internal long AmountMinorUnits { get; }
    internal Currency Currency { get; }

    private PartyPurchaseInstallment(Guid id, Guid partyPurchaseId, int number, long amountMinorUnits, Currency currency, DateOnly dueOn, Guid? ledgerTransactionId) : base(id) {
        PartyPurchaseId = partyPurchaseId;
        Number = number;
        AmountMinorUnits = amountMinorUnits;
        Currency = currency;
        DueOn = dueOn;
        LedgerTransactionId = ledgerTransactionId;
    }

    internal static PartyPurchaseInstallment Posted(Guid partyPurchaseId, int number, Money amount, DateOnly dueOn, Guid ledgerTransactionId) {
        return new PartyPurchaseInstallment(Guid.CreateVersion7(), partyPurchaseId, number, amount.MinorUnits, amount.Currency, dueOn, ledgerTransactionId);
    }

    internal static PartyPurchaseInstallment Scheduled(Guid partyPurchaseId, int number, Money amount, DateOnly dueOn) {
        return new PartyPurchaseInstallment(Guid.CreateVersion7(), partyPurchaseId, number, amount.MinorUnits, amount.Currency, dueOn, null);
    }

    internal void MarkPosted(Guid ledgerTransactionId) {
        LedgerTransactionId = ledgerTransactionId;
    }
}

using PersonalFinance.SharedKernel;

namespace PersonalFinance.Parties.Domain;

internal enum PartyPurchaseKind {
    Debit,
    Credit
}

/// <summary>
/// A purchase a party paid where I took part; my share is paid off through one or more installments.
/// </summary>
internal sealed class PartyPurchase : AggregateRoot<Guid> {
    public Guid PartyId { get; }
    public string Description { get; }
    public string CategoryName { get; }
    public PartyPurchaseKind Kind { get; }
    public DateOnly PurchaseDate { get; }
    public bool IsCancelled { get; private set; }
    public IReadOnlyList<PartyPurchaseInstallment> Installments => installments;

    internal long ShareMinorUnits { get; }
    internal Currency Currency { get; }

    private readonly List<PartyPurchaseInstallment> installments = [];

    private PartyPurchase(Guid id, Guid partyId, string description, string categoryName, PartyPurchaseKind kind, DateOnly purchaseDate, long shareMinorUnits, Currency currency) : base(id) {
        PartyId = partyId;
        Description = description;
        CategoryName = categoryName;
        Kind = kind;
        PurchaseDate = purchaseDate;
        ShareMinorUnits = shareMinorUnits;
        Currency = currency;
    }

    public static PartyPurchase Debit(Guid partyId, string description, string categoryName, DateOnly purchaseDate, Money share, Guid ledgerTransactionId) {
        var purchase = new PartyPurchase(Guid.CreateVersion7(), partyId, description, categoryName, PartyPurchaseKind.Debit, purchaseDate, share.MinorUnits, share.Currency);
        purchase.installments.Add(PartyPurchaseInstallment.Posted(purchase.Id, 1, share, purchaseDate, ledgerTransactionId));
        return purchase;
    }

    public static PartyPurchase Credit(Guid partyId, string description, string categoryName, DateOnly purchaseDate, Money sharePerInstallment, int installmentCount, DateOnly firstPaymentMonth) {
        var purchase = new PartyPurchase(Guid.CreateVersion7(), partyId, description, categoryName, PartyPurchaseKind.Credit, purchaseDate, sharePerInstallment.MinorUnits, sharePerInstallment.Currency);
        var firstDue = new DateOnly(firstPaymentMonth.Year, firstPaymentMonth.Month, 1);
        for(var number = 1; number <= installmentCount; number++) {
            purchase.installments.Add(PartyPurchaseInstallment.Scheduled(purchase.Id, number, sharePerInstallment, firstDue.AddMonths(number - 1)));
        }
        return purchase;
    }

    public void Cancel() {
        IsCancelled = true;
    }
}

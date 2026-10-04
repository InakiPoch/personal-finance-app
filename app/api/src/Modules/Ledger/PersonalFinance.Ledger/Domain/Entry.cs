using PersonalFinance.Ledger.Contracts;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Ledger.Domain;

///<summary>
/// One part of a <see cref="Transaction"/>.
/// </summary>
internal sealed class Entry : Entity<Guid> {
    public Guid AccountId { get; }
    public DebitOrCredit Direction { get; }
    public Money Amount => Money.FromMinorUnits(AmountMinorUnits, Currency);

    internal long AmountMinorUnits { get; }
    internal Currency Currency { get; }

    internal Entry(Guid id, Guid accountId, DebitOrCredit direction, long amountMinorUnits, Currency currency) : base(id) {
        AccountId = accountId;
        Direction = direction;
        AmountMinorUnits = amountMinorUnits;
        Currency = currency;
    }
}

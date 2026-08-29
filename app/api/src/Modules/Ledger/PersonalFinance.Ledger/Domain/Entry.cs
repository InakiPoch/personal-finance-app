using PersonalFinance.Ledger.Contracts;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Ledger.Domain;

/// <summary>
/// One part of a <see cref="Transaction"/>.
/// </summary>
internal sealed class Entry : Entity<Guid> {
    public Guid AccountId { get; }
    public DebitOrCredit Direction { get; }
    public Money Amount { get; }

    internal Entry(Guid id, Guid accountId, DebitOrCredit direction, Money amount) : base(id) {
        AccountId = accountId;
        Direction = direction;
        Amount = amount;
    }
}

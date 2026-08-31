using PersonalFinance.SharedKernel;

namespace PersonalFinance.Parties.Domain;

/// <summary>
/// A party's persisted slice of one <see cref="ExpenseSplit"/>.
/// </summary>
internal sealed class ExpenseSplitParticipant : Entity<Guid> {
    public Guid ExpenseSplitId { get; }
    public Guid PartyId { get; }
    public Money Share { get; }

    private ExpenseSplitParticipant(Guid id, Guid expenseSplitId, Guid partyId, Money share) : base(id) {
        ExpenseSplitId = expenseSplitId;
        PartyId = partyId;
        Share = share;
    }

    internal static ExpenseSplitParticipant For(Guid expenseSplitId, Guid partyId, Money share) {
        return new ExpenseSplitParticipant(Guid.CreateVersion7(), expenseSplitId, partyId, share);
    }
}

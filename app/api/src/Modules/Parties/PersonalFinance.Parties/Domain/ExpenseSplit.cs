using PersonalFinance.SharedKernel;

namespace PersonalFinance.Parties.Domain;

internal sealed class ExpenseSplit : AggregateRoot<Guid> {
    public ExpenseSplitSource Source { get; }
    public Guid SourceReferenceId { get; }
    public Money Total => Money.FromMinorUnits(TotalMinorUnits, Currency);
    public Money HolderShare => Money.FromMinorUnits(HolderShareMinorUnits, Currency);
    public Money AccruedReceivable => Money.FromMinorUnits(AccruedReceivableMinorUnits, Currency);
    public Money ReversedReceivable => Money.FromMinorUnits(ReversedReceivableMinorUnits, Currency);
    public IReadOnlyList<ExpenseSplitParticipant> Participants => participants;
    public Money PartyReceivableTotal => participants.Aggregate(Money.Zero(Currency), (running, participant) => running + participant.Share);

    internal long TotalMinorUnits { get; }
    internal long HolderShareMinorUnits { get; }
    internal long AccruedReceivableMinorUnits { get; private set; }
    internal long ReversedReceivableMinorUnits { get; private set; }
    internal Currency Currency { get; }

    private readonly List<ExpenseSplitParticipant> participants = [];

    private ExpenseSplit(Guid id, ExpenseSplitSource source, Guid sourceReferenceId, long totalMinorUnits, long holderShareMinorUnits, long accruedReceivableMinorUnits, Currency currency) : base(id) {
        Source = source;
        SourceReferenceId = sourceReferenceId;
        TotalMinorUnits = totalMinorUnits;
        HolderShareMinorUnits = holderShareMinorUnits;
        AccruedReceivableMinorUnits = accruedReceivableMinorUnits;
        Currency = currency;
        ReversedReceivableMinorUnits = 0;
    }

    public static Result<ExpenseSplit> Create(ExpenseSplitSource source, Guid sourceReferenceId, Money total, Money holderShare, IReadOnlyList<PartyShare> participantShares, Money accruedReceivable) {
        if(total.MinorUnits <= 0) {
            return PartiesErrors.NonPositiveAmount;
        }
        if(participantShares.Count == 0) {
            return PartiesErrors.InvalidParticipants;
        }
        var partyPortion = participantShares.Aggregate(
            Money.Zero(total.Currency), (running, share) => running + share.Share);
        if((holderShare + partyPortion).MinorUnits != total.MinorUnits) {
            throw new InvalidOperationException(
                "Holder share plus participant shares must reconcile to the split total.");
        }
        var split = new ExpenseSplit(Guid.CreateVersion7(), source, sourceReferenceId, total.MinorUnits, holderShare.MinorUnits, accruedReceivable.MinorUnits, total.Currency);
        foreach(var share in participantShares) {
            split.participants.Add(ExpenseSplitParticipant.For(split.Id, share.PartyId, share.Share));
        }
        return split;
    }

    public Result RecordAccrued(Money amount) {
        if(amount.MinorUnits <= 0) {
            return Result.Failure(PartiesErrors.NonPositiveAmount);
        }
        AccruedReceivableMinorUnits = (AccruedReceivable + amount).MinorUnits;
        return Result.Success();
    }

    public Result RecordReversed(Money amount) {
        if(amount.MinorUnits <= 0) {
            return Result.Failure(PartiesErrors.NonPositiveAmount);
        }
        ReversedReceivableMinorUnits = (ReversedReceivable + amount).MinorUnits;
        return Result.Success();
    }
}

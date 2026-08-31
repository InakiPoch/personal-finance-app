using PersonalFinance.SharedKernel;

namespace PersonalFinance.Parties.Domain;

internal sealed class ExpenseSplit : AggregateRoot<Guid> {
    public ExpenseSplitSource Source { get; }
    public Guid SourceReferenceId { get; }
    public Money Total { get; }
    public Money HolderShare { get; }
    public Money AccruedReceivable { get; private set; }
    public Money ReversedReceivable { get; private set; }
    public IReadOnlyList<ExpenseSplitParticipant> Participants => participants;
    public Money PartyReceivableTotal => participants.Aggregate(Money.Zero(Total.Currency), (running, participant) => running + participant.Share);

    private readonly List<ExpenseSplitParticipant> participants = [];

    private ExpenseSplit(Guid id, ExpenseSplitSource source, Guid sourceReferenceId, Money total, Money holderShare, Money accruedReceivable) : base(id) {
        Source = source;
        SourceReferenceId = sourceReferenceId;
        Total = total;
        HolderShare = holderShare;
        AccruedReceivable = accruedReceivable;
        ReversedReceivable = Money.Zero(total.Currency);
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
        var split = new ExpenseSplit(Guid.CreateVersion7(), source, sourceReferenceId, total, holderShare, accruedReceivable);
        foreach(var share in participantShares) {
            split.participants.Add(ExpenseSplitParticipant.For(split.Id, share.PartyId, share.Share));
        }
        return split;
    }

    public Result RecordAccrued(Money amount) {
        if(amount.MinorUnits <= 0) {
            return Result.Failure(PartiesErrors.NonPositiveAmount);
        }
        AccruedReceivable += amount;
        return Result.Success();
    }

    public Result RecordReversed(Money amount) {
        if(amount.MinorUnits <= 0) {
            return Result.Failure(PartiesErrors.NonPositiveAmount);
        }
        ReversedReceivable += amount;
        return Result.Success();
    }
}

using PersonalFinance.SharedKernel;
using PersonalFinance.SharedKernel.Allocation;

namespace PersonalFinance.Financing.Domain;

internal sealed class PaymentPlan : AggregateRoot<Guid> {
    public Guid CardId { get; }
    public Money Total { get; }
    public DateOnly PurchaseDate { get; }
    public int InstallmentCount { get; }
    public Guid? SplitReferenceId { get; private set; }
    public IReadOnlyList<Installment> Installments => installments;
    public IReadOnlyList<PaymentPlanSplitParticipant> SplitParticipants => splitParticipants;

    private readonly List<Installment> installments = [];
    private readonly List<PaymentPlanSplitParticipant> splitParticipants = [];

    private PaymentPlan(Guid id, Guid cardId, Money total, DateOnly purchaseDate, int installmentCount) : base(id) {
        CardId = cardId;
        Total = total;
        PurchaseDate = purchaseDate;
        InstallmentCount = installmentCount;
    }

    public static Result<PaymentPlan> Create(
        Guid cardId,
        Money total,
        int installmentCount,
        DateOnly purchaseDate,
        int cutoffDay,
        PhantomPennyAllocator allocator,
        IReadOnlyList<(Guid PartyId, long Weight)>? splitParticipants = null) {
        if(total.MinorUnits <= 0) {
            return FinancingErrors.NonPositivePlanAmount;
        }
        if(installmentCount < 1) {
            return FinancingErrors.InvalidInstallmentCount;
        }
        var plan = new PaymentPlan(Guid.CreateVersion7(), cardId, total, purchaseDate, installmentCount);
        var weights = Enumerable.Repeat(1L, installmentCount).ToArray();
        var shares = allocator.Allocate(total, weights);
        var firstCycle = BillingCycleCalculator.ResolveCycle(purchaseDate, cutoffDay);
        for(var i = 0; i < installmentCount; i++) {
            plan.installments.Add(Installment.Schedule(plan.Id, i + 1, shares[i], firstCycle.AddMonths(i)));
        }
        if(splitParticipants is not null) {
            foreach(var participant in splitParticipants) {
                plan.splitParticipants.Add(
                    PaymentPlanSplitParticipant.For(plan.Id, participant.PartyId, participant.Weight));
            }
        }
        return plan;
    }

    public Result LinkSplit(Guid splitReferenceId, IReadOnlyDictionary<Guid, Guid> receivableAccountsByParty) {
        if(SplitReferenceId is not null) {
            return Result.Success();
        }
        SplitReferenceId = splitReferenceId;
        foreach(var participant in splitParticipants) {
            if(receivableAccountsByParty.TryGetValue(participant.PartyId, out var receivableAccountId)) {
                participant.AssignReceivableAccount(receivableAccountId);
            }
        }
        return Result.Success();
    }
}

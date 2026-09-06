using PersonalFinance.SharedKernel;
using PersonalFinance.SharedKernel.Allocation;

namespace PersonalFinance.Financing.Domain;

internal sealed class PaymentPlan : AggregateRoot<Guid> {
    public Guid? CardId { get; }
    public Money Total { get; }
    public DateOnly PurchaseDate { get; }
    public int InstallmentCount { get; }
    public string Description { get; private set; } = string.Empty;
    public Guid? SplitReferenceId { get; private set; }
    public Guid? CreditorId { get; private set; }
    public Guid? CreditorAccountId { get; private set; }
    public Guid? CreditorPayableAccountId { get; private set; }
    public IReadOnlyList<Installment> Installments => installments;
    public IReadOnlyList<PaymentPlanSplitParticipant> SplitParticipants => splitParticipants;

    private readonly List<Installment> installments = [];
    private readonly List<PaymentPlanSplitParticipant> splitParticipants = [];

    private PaymentPlan(Guid id, Guid? cardId, Money total, DateOnly purchaseDate, int installmentCount) : base(id) {
        CardId = cardId;
        Total = total;
        PurchaseDate = purchaseDate;
        InstallmentCount = installmentCount;
    }

    public static Result<PaymentPlan> Create(
        Guid? cardId,
        Money total,
        int installmentCount,
        DateOnly purchaseDate,
        string description,
        int? cutoffDay,
        PhantomPennyAllocator allocator,
        IReadOnlyList<(Guid PartyId, long Weight)>? splitParticipants = null,
        Guid? creditorId = null,
        Guid? creditorAccountId = null) {
        if(total.MinorUnits <= 0) {
            return FinancingErrors.NonPositivePlanAmount;
        }
        if(installmentCount < 1) {
            return FinancingErrors.InvalidInstallmentCount;
        }
        if(cardId is null && creditorId is null) {
            return FinancingErrors.PlanNeedsCardOrCreditor;
        }
        if(cardId is not null && creditorId is not null) {
            return FinancingErrors.PlanCannotMixCardAndCreditor;
        }
        if(cardId is null && creditorAccountId is null) {
            return FinancingErrors.CreditorAccountRequired;
        }
        var plan = new PaymentPlan(Guid.CreateVersion7(), cardId, total, purchaseDate, installmentCount) {
            Description = description.Trim(),
            CreditorId = creditorId,
            CreditorAccountId = creditorAccountId
        };
        var weights = Enumerable.Repeat(1L, installmentCount).ToArray();
        var shares = allocator.Allocate(total, weights);
        var firstCycle = cardId is not null
            ? BillingCycleCalculator.ResolveCycle(purchaseDate, cutoffDay!.Value)
            : new BillingCycle(purchaseDate.Year, purchaseDate.Month);
        for(var i = 0; i < installmentCount; i++) {
            plan.installments.Add(Installment.Schedule(plan.Id, i + 1, shares[i], firstCycle.AddMonths(i)));
        }
        if (splitParticipants is null) return plan;
        foreach(var participant in splitParticipants) {
            plan.splitParticipants.Add(PaymentPlanSplitParticipant.For(plan.Id, participant.PartyId, participant.Weight));
        }
        return plan;
    }

    public void AssignCreditorPayableAccount(Guid accountId) {
        CreditorPayableAccountId = accountId;
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

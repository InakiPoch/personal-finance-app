using PersonalFinance.SharedKernel;
using PersonalFinance.SharedKernel.Allocation;

namespace PersonalFinance.Financing.Domain;

internal sealed class PaymentPlan : AggregateRoot<Guid> {
    public Guid CardId { get; }
    public Money Total { get; }
    public DateOnly PurchaseDate { get; }
    public int InstallmentCount { get; }
    public IReadOnlyList<Installment> Installments => installments;

    private readonly List<Installment> installments = [];

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
        PhantomPennyAllocator allocator) {
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
        return plan;
    }
}

using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Domain;

internal sealed class Installment : Entity<Guid> {
    public Guid PaymentPlanId { get; }
    public int Sequence { get; }
    public Money Amount { get; }
    public int CycleYear { get; }
    public int CycleMonth { get; }
    public DateTimeOffset? AccruedOnUtc { get; private set; }
    public Guid? StatementId { get; private set; }
    public bool IsReversed { get; private set; }
    public bool IsAccrued => AccruedOnUtc is not null;
    public BillingCycle Cycle => new(CycleYear, CycleMonth);

    private Installment(Guid id, Guid paymentPlanId, int sequence, Money amount, int cycleYear, int cycleMonth) : base(id) {
        PaymentPlanId = paymentPlanId;
        Sequence = sequence;
        Amount = amount;
        CycleYear = cycleYear;
        CycleMonth = cycleMonth;
    }

    internal static Installment Schedule(Guid paymentPlanId, int sequence, Money amount, BillingCycle cycle) {
        return new Installment(Guid.CreateVersion7(), paymentPlanId, sequence, amount, cycle.Year, cycle.Month);
    }

    public Result MarkAccrued(DateTimeOffset accruedOnUtc, MonthlyStatement statement) {
        if(AccruedOnUtc is not null) {
            return Result.Failure(FinancingErrors.InstallmentAlreadyAccrued);
        }
        AccruedOnUtc = accruedOnUtc;
        StatementId = statement.Id;
        return Result.Success();
    }

    // Creditor-financed plans have no MonthlyStatement — the installment is accrued straight to the
    // Ledger split legs (or to nothing, when the holder carries the whole cuota), so StatementId stays null.
    public Result MarkCreditorAccrued(DateTimeOffset accruedOnUtc) {
        if(AccruedOnUtc is not null) {
            return Result.Failure(FinancingErrors.InstallmentAlreadyAccrued);
        }
        AccruedOnUtc = accruedOnUtc;
        return Result.Success();
    }

    public Result MarkReversed() {
        if(IsReversed) {
            return Result.Failure(FinancingErrors.InstallmentAlreadyReversed);
        }
        IsReversed = true;
        return Result.Success();
    }
}

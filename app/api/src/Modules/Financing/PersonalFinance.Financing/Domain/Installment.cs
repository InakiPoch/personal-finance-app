using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Domain;

internal sealed class Installment : Entity<Guid> {
    public Guid PaymentPlanId { get; }
    public int Sequence { get; }
    public Money Amount => Money.FromMinorUnits(AmountMinorUnits, Currency);
    public int CycleYear { get; }
    public int CycleMonth { get; }
    public DateTimeOffset? AccruedOnUtc { get; private set; }
    public DateTimeOffset? SplitAccruedOnUtc { get; private set; }
    public DateTimeOffset? PaidOnUtc { get; private set; }
    public Guid? StatementId { get; private set; }
    public bool IsReversed { get; private set; }
    public bool IsAccrued => AccruedOnUtc is not null;
    public bool IsSplitAccrued => SplitAccruedOnUtc is not null;
    public bool IsPaid => PaidOnUtc is not null;
    public BillingCycle Cycle => new(CycleYear, CycleMonth);
    public BillingCycle DueCycle => Cycle.DueCycle;
    public IReadOnlyList<CreditorInstallmentPayment> Payments => payments;
    public long PaidMinorUnits => payments.Sum(payment => payment.AmountMinorUnits);
    public long RemainingMinorUnits => AmountMinorUnits - PaidMinorUnits;

    internal long AmountMinorUnits { get; }
    internal Currency Currency { get; }

    private readonly List<CreditorInstallmentPayment> payments = [];

    private Installment(Guid id, Guid paymentPlanId, int sequence, long amountMinorUnits, Currency currency, int cycleYear, int cycleMonth) : base(id) {
        PaymentPlanId = paymentPlanId;
        Sequence = sequence;
        AmountMinorUnits = amountMinorUnits;
        Currency = currency;
        CycleYear = cycleYear;
        CycleMonth = cycleMonth;
    }

    internal static Installment Schedule(Guid paymentPlanId, int sequence, Money amount, BillingCycle cycle) {
        return new Installment(Guid.CreateVersion7(), paymentPlanId, sequence, amount.MinorUnits, amount.Currency, cycle.Year, cycle.Month);
    }

    public Result MarkAccrued(DateTimeOffset accruedOnUtc, MonthlyStatement statement) {
        if(AccruedOnUtc is not null) {
            return Result.Failure(FinancingErrors.InstallmentAlreadyAccrued);
        }
        AccruedOnUtc = accruedOnUtc;
        StatementId = statement.Id;
        return Result.Success();
    }

    public Result MarkSplitAccrued(DateTimeOffset splitAccruedOnUtc) {
        if(SplitAccruedOnUtc is not null) {
            return Result.Failure(FinancingErrors.InstallmentSplitAlreadyAccrued);
        }
        SplitAccruedOnUtc = splitAccruedOnUtc;
        return Result.Success();
    }

    public Result MarkPaid(DateTimeOffset paidOnUtc) {
        if(IsPaid) {
            return Result.Failure(FinancingErrors.InstallmentAlreadyPaid);
        }
        PaidOnUtc = paidOnUtc;
        return Result.Success();
    }

    public Result ClearPayment() {
        PaidOnUtc = null;
        return Result.Success();
    }

    public Result<CreditorInstallmentPayment> ApplyPayment(long amountMinorUnits, DateTimeOffset now, Guid? partyId = null, Guid? settlementTransactionId = null) {
        if(IsReversed) {
            return FinancingErrors.InstallmentAlreadyReversed;
        }
        if(RemainingMinorUnits == 0) {
            return FinancingErrors.InstallmentAlreadyPaid;
        }
        if(amountMinorUnits <= 0) {
            return FinancingErrors.InvalidPaymentAmount;
        }
        if(amountMinorUnits > RemainingMinorUnits) {
            return FinancingErrors.PaymentExceedsRemaining;
        }
        var payment = CreditorInstallmentPayment.For(Id, amountMinorUnits, now, partyId, settlementTransactionId);
        payments.Add(payment);
        if(RemainingMinorUnits == 0) {
            MarkPaid(now);
        }
        return payment;
    }

    public Result<CreditorInstallmentPayment> UndoLastPayment() {
        if(payments.Count == 0) {
            return FinancingErrors.NoPaymentToUndo;
        }
        var last = payments.OrderByDescending(payment => payment.PaidOnUtc).ThenByDescending(payment => payment.Id).First();
        payments.Remove(last);
        if(PaidOnUtc is not null) {
            ClearPayment();
        }
        return last;
    }

    public Result MarkReversed() {
        if(IsReversed) {
            return Result.Failure(FinancingErrors.InstallmentAlreadyReversed);
        }
        IsReversed = true;
        return Result.Success();
    }
}

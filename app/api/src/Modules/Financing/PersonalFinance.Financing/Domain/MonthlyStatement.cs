using PersonalFinance.Financing.Domain.Events;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Domain;

internal sealed class MonthlyStatement : AggregateRoot<Guid> {
    public Guid CardId { get; }
    public int CycleYear { get; }
    public int CycleMonth { get; }
    public Money AmountDue { get; private set; }
    public DateTimeOffset? PaidOnUtc { get; private set; }
    public bool IsPaid => PaidOnUtc is not null;
    public BillingCycle Cycle => new(CycleYear, CycleMonth);

    private MonthlyStatement(Guid id, Guid cardId, int cycleYear, int cycleMonth) : base(id) {
        CardId = cardId;
        CycleYear = cycleYear;
        CycleMonth = cycleMonth;
        AmountDue = Money.Zero(Currency.Reference);
    }

    public static MonthlyStatement Open(Guid cardId, BillingCycle cycle) {
        return new MonthlyStatement(Guid.CreateVersion7(), cardId, cycle.Year, cycle.Month);
    }

    public Result Accrue(Installment installment) {
        if(IsPaid) {
            return Result.Failure(FinancingErrors.StatementAlreadyPaid);
        }
        AmountDue += installment.Amount;
        RaiseDomainEvent(new InstallmentAccrued(installment.Id, Id, installment.Amount.MinorUnits));
        return Result.Success();
    }

    public Result MarkPaid(DateTimeOffset paidOnUtc) {
        if(IsPaid) {
            return Result.Failure(FinancingErrors.StatementAlreadyPaid);
        }
        PaidOnUtc = paidOnUtc;
        return Result.Success();
    }
}

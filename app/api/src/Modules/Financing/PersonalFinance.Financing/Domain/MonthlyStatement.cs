using PersonalFinance.Financing.Domain.Events;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Domain;

internal sealed class MonthlyStatement : AggregateRoot<Guid> {
    public Guid CardId { get; }
    public int CycleYear { get; }
    public int CycleMonth { get; }
    public Money AmountDue => Money.FromMinorUnits(AmountDueMinorUnits, Currency);
    public DateTimeOffset? PaidOnUtc { get; private set; }
    public bool IsPaid => PaidOnUtc is not null;
    public BillingCycle Cycle => new(CycleYear, CycleMonth);

    internal long AmountDueMinorUnits { get; private set; }
    internal Currency Currency { get; private set; }

    private MonthlyStatement(Guid id, Guid cardId, int cycleYear, int cycleMonth, long amountDueMinorUnits, Currency currency) : base(id) {
        CardId = cardId;
        CycleYear = cycleYear;
        CycleMonth = cycleMonth;
        AmountDueMinorUnits = amountDueMinorUnits;
        Currency = currency;
    }

    public static MonthlyStatement Open(Guid cardId, BillingCycle cycle, Currency currency) {
        return new MonthlyStatement(Guid.CreateVersion7(), cardId, cycle.Year, cycle.Month, 0, currency);
    }

    public Result Accrue(Installment installment) {
        if(IsPaid) {
            return Result.Failure(FinancingErrors.StatementAlreadyPaid);
        }
        AmountDueMinorUnits = (AmountDue + installment.Amount).MinorUnits;
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

    /// <summary>
    /// The statement is fully paid once every accrued, non-reversed installment it holds carries its own <see cref="Installment.PaidOnUtc"/>.
    /// </summary>
    public bool IsFullyPaidBy(IEnumerable<Installment> statementInstallments) {
        return statementInstallments
            .Where(installment => installment.IsAccrued && !installment.IsReversed)
            .All(installment => installment.IsPaid);
    }
}

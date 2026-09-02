using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Domain;

internal sealed class CreditCard : AggregateRoot<Guid> {
    public string Name { get; }
    public int CutoffDay { get; }
    public Guid LiabilityAccountId { get; }
    public Guid ExpenseAccountId { get; }
    public Guid CreditAccountId { get; }
    public Money CarriedCreditBalance { get; private set; }

    private CreditCard(Guid id, string name, int cutoffDay, Guid liabilityAccountId, Guid expenseAccountId, Guid creditAccountId) : base(id) {
        Name = name;
        CutoffDay = cutoffDay;
        LiabilityAccountId = liabilityAccountId;
        ExpenseAccountId = expenseAccountId;
        CreditAccountId = creditAccountId;
        CarriedCreditBalance = Money.Zero(Currency.Reference);
    }

    public static Result<CreditCard> Create(Guid id, string name, int cutoffDay, Guid liabilityAccountId, Guid expenseAccountId, Guid creditAccountId) {
        if(string.IsNullOrWhiteSpace(name)) {
            return FinancingErrors.InvalidCardName;
        }
        if(cutoffDay is < 1 or > 31) {
            return FinancingErrors.InvalidCutoffDay;
        }
        return new CreditCard(id, name.Trim(), cutoffDay, liabilityAccountId, expenseAccountId, creditAccountId);
    }

    public Result ApplyCredit(Money amount) {
        if(amount.MinorUnits <= 0) {
            return Result.Failure(FinancingErrors.NonPositiveCreditAmount);
        }
        CarriedCreditBalance += amount;
        return Result.Success();
    }

    public Result ConsumeCredit(Money amount) {
        if(amount.MinorUnits <= 0) {
            return Result.Failure(FinancingErrors.NonPositiveCreditAmount);
        }
        var reduced = CarriedCreditBalance - amount;
        CarriedCreditBalance = reduced.MinorUnits < 0 ? Money.Zero(Currency.Reference) : reduced;
        return Result.Success();
    }
}

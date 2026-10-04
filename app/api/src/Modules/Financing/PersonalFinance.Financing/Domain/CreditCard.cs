using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Domain;

internal sealed class CreditCard : AggregateRoot<Guid> {
    public string Name { get; }
    public int CutoffDay { get; private set; }
    public Guid LiabilityAccountId { get; }
    public Guid ExpenseAccountId { get; }
    public Guid CreditAccountId { get; }
    public IReadOnlyList<ClosingOverride> ClosingOverrides => closingOverrides;
    public Money CarriedCreditBalance => Money.FromMinorUnits(CarriedCreditBalanceMinorUnits, Currency);

    internal long CarriedCreditBalanceMinorUnits { get; private set; }
    internal Currency Currency { get; private set; }

    private readonly List<ClosingOverride> closingOverrides = [];

    private CreditCard(Guid id, string name, int cutoffDay, Guid liabilityAccountId, Guid expenseAccountId, Guid creditAccountId, long carriedCreditBalanceMinorUnits, Currency currency) : base(id) {
        Name = name;
        CutoffDay = cutoffDay;
        LiabilityAccountId = liabilityAccountId;
        ExpenseAccountId = expenseAccountId;
        CreditAccountId = creditAccountId;
        CarriedCreditBalanceMinorUnits = carriedCreditBalanceMinorUnits;
        Currency = currency;
    }

    public static Result<CreditCard> Create(Guid id, string name, int cutoffDay, Guid liabilityAccountId, Guid expenseAccountId, Guid creditAccountId) {
        if(string.IsNullOrWhiteSpace(name)) {
            return FinancingErrors.InvalidCardName;
        }
        if(cutoffDay is < 1 or > 31) {
            return FinancingErrors.InvalidCutoffDay;
        }
        return new CreditCard(id, name.Trim(), cutoffDay, liabilityAccountId, expenseAccountId, creditAccountId, 0, Currency.Reference);
    }

    public Result ChangeUsualClosingDay(int day) {
        if(day is < 1 or > 31) {
            return Result.Failure(FinancingErrors.InvalidCutoffDay);
        }
        CutoffDay = day;
        return Result.Success();
    }
    
    public Result SetClosingDay(BillingCycle cycle, int day) {
        if(day < 1 || day > DateTime.DaysInMonth(cycle.Year, cycle.Month)) {
            return Result.Failure(FinancingErrors.InvalidClosingDay);
        }
        var existing = closingOverrides.FirstOrDefault(candidate => candidate.Cycle == cycle);
        if(existing is null) {
            closingOverrides.Add(ClosingOverride.For(Id, cycle, day));
        } else {
            existing.ChangeDay(day);
        }
        return Result.Success();
    }

    public Result ClearClosingDay(BillingCycle cycle) {
        var existing = closingOverrides.FirstOrDefault(candidate => candidate.Cycle == cycle);
        if(existing is not null) {
            closingOverrides.Remove(existing);
        }
        return Result.Success();
    }

    public bool HasOverrideFor(BillingCycle cycle) {
        return closingOverrides.Any(candidate => candidate.Cycle == cycle);
    }

    public int ClosingDayOf(BillingCycle cycle) {
        var day = closingOverrides.FirstOrDefault(candidate => candidate.Cycle == cycle)?.ClosingDay ?? CutoffDay;
        return Math.Min(day, DateTime.DaysInMonth(cycle.Year, cycle.Month));
    }

    public DateOnly ClosingDateOf(BillingCycle cycle) {
        return new DateOnly(cycle.Year, cycle.Month, ClosingDayOf(cycle));
    }

    public bool IsClosedAsOf(BillingCycle cycle, DateOnly today) {
        return today > ClosingDateOf(cycle);
    }

    public BillingCycle ResolveCycle(DateOnly purchaseDate) {
        var own = new BillingCycle(purchaseDate.Year, purchaseDate.Month);
        return purchaseDate.Day <= ClosingDayOf(own) ? own : own.AddMonths(1);
    }

    public Result ApplyCredit(Money amount) {
        if(amount.MinorUnits <= 0) {
            return Result.Failure(FinancingErrors.NonPositiveCreditAmount);
        }
        CarriedCreditBalanceMinorUnits = (CarriedCreditBalance + amount).MinorUnits;
        return Result.Success();
    }

    public Result ConsumeCredit(Money amount) {
        if(amount.MinorUnits <= 0) {
            return Result.Failure(FinancingErrors.NonPositiveCreditAmount);
        }
        var reduced = CarriedCreditBalance - amount;
        CarriedCreditBalanceMinorUnits = Math.Max(reduced.MinorUnits, 0);
        return Result.Success();
    }
}

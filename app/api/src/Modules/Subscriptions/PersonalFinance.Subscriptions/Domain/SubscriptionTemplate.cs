using PersonalFinance.SharedKernel;
using PersonalFinance.Subscriptions.Contracts;

namespace PersonalFinance.Subscriptions.Domain;

/// <summary>
/// A recurring charge definition.
/// </summary>
internal sealed class SubscriptionTemplate : AggregateRoot<Guid> {
    public string Name { get; }
    public Money Amount { get; }
    public string Category { get; }
    public Guid ExpenseAccountId { get; }
    public Guid FundingAccountId { get; }
    public RecurrenceFrequency Frequency { get; }
    public int AnchorDay { get; }
    public DateOnly NextDueDate { get; private set; }
    public bool IsActive { get; private set; }
    public DateOnly? LastPaidPeriod { get; private set; }
    public RecurrenceRule Recurrence => new(Frequency, AnchorDay);
    public RenewalSchedule Schedule => new(NextDueDate, IsActive);

    private SubscriptionTemplate(
        Guid id,
        string name,
        Money amount,
        string category,
        Guid expenseAccountId,
        Guid fundingAccountId,
        RecurrenceFrequency frequency,
        int anchorDay,
        DateOnly nextDueDate,
        DateOnly? lastPaidPeriod) : base(id) {
        Name = name;
        Amount = amount;
        Category = category;
        ExpenseAccountId = expenseAccountId;
        FundingAccountId = fundingAccountId;
        Frequency = frequency;
        AnchorDay = anchorDay;
        NextDueDate = nextDueDate;
        IsActive = true;
        LastPaidPeriod = lastPaidPeriod;
    }

    public static Result<SubscriptionTemplate> Create(
        string name,
        Money amount,
        string category,
        Guid expenseAccountId,
        Guid fundingAccountId,
        RecurrenceFrequency frequency,
        int anchorDay,
        DateOnly nextDueDate) {
        if(string.IsNullOrWhiteSpace(name)) {
            return SubscriptionErrors.InvalidName;
        }
        if(string.IsNullOrWhiteSpace(category)) {
            return SubscriptionErrors.InvalidCategory;
        }
        if(amount.MinorUnits <= 0) {
            return SubscriptionErrors.NonPositiveAmount;
        }
        if(anchorDay is < 1 or > 31) {
            return SubscriptionErrors.InvalidAnchorDay;
        }
        if(expenseAccountId == Guid.Empty || fundingAccountId == Guid.Empty) {
            return SubscriptionErrors.InvalidFundingAccount;
        }
        return new SubscriptionTemplate(
            Guid.CreateVersion7(),
            name.Trim(),
            amount,
            category.Trim(),
            expenseAccountId,
            fundingAccountId,
            frequency,
            anchorDay,
            nextDueDate,
            null
        );
    }

    public Result MarkCurrentPeriodPaid(DateOnly paidPeriodAnchor) {
        if(!IsActive) {
            return Result.Failure(SubscriptionErrors.SubscriptionNotActive);
        }
        LastPaidPeriod = paidPeriodAnchor;
        NextDueDate = Recurrence.Next(NextDueDate);
        return Result.Success();
    }

    public Result RevertLastPayment() {
        if(!IsActive) {
            return Result.Failure(SubscriptionErrors.SubscriptionNotActive);
        }
        LastPaidPeriod = LastPaidPeriod?.AddMonths(-1);
        NextDueDate = NextDueDate.AddMonths(-1);
        return Result.Success();
    }

    public Result Cancel() {
        IsActive = false;
        return Result.Success();
    }
}

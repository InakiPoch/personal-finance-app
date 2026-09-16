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
    public DateTimeOffset? LastRenewalOnUtc { get; private set; }
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
        DateTimeOffset? lastRenewalOnUtc) : base(id) {
        Name = name;
        Amount = amount;
        Category = category;
        ExpenseAccountId = expenseAccountId;
        FundingAccountId = fundingAccountId;
        Frequency = frequency;
        AnchorDay = anchorDay;
        NextDueDate = nextDueDate;
        IsActive = true;
        LastRenewalOnUtc = lastRenewalOnUtc;
    }

    public static Result<SubscriptionTemplate> Create(
        string name,
        Money amount,
        string category,
        Guid expenseAccountId,
        Guid fundingAccountId,
        RecurrenceFrequency frequency,
        int anchorDay,
        DateOnly nextDueDate,
        DateTimeOffset firstChargeOnUtc) {
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
            firstChargeOnUtc
        );
    }

    public Result Cancel() {
        IsActive = false;
        return Result.Success();
    }
}

namespace PersonalFinance.Subscriptions.Domain;

internal sealed record RenewalSchedule(DateOnly NextDueDate, bool IsActive) {
    public RenewalSchedule Advance(RecurrenceRule rule) {
        return new RenewalSchedule(rule.Next(NextDueDate), IsActive);
    }

    public RenewalSchedule Deactivate() {
        return new RenewalSchedule(NextDueDate, false);
    }
}

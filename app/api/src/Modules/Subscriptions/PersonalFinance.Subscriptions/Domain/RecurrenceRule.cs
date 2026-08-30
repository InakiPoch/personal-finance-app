using PersonalFinance.SharedKernel;
using PersonalFinance.Subscriptions.Contracts;

namespace PersonalFinance.Subscriptions.Domain;

/// <summary>
/// How a subscription recurs: a frequency plus the day-of-month it anchors to. Rebuilt from the
/// aggregate's flat columns, exactly as <c>Installment.Cycle</c> is.
/// </summary>
internal sealed record RecurrenceRule(RecurrenceFrequency Frequency, int AnchorDay) {
    public static Result<RecurrenceRule> Create(RecurrenceFrequency frequency, int anchorDay) {
        if(anchorDay is < 1 or > 31) {
            return SubscriptionErrors.InvalidAnchorDay;
        }
        return new RecurrenceRule(frequency, anchorDay);
    }

    public DateOnly Next(DateOnly after) {
        var candidate = onAnchorDay(after.Year, after.Month);
        if(candidate > after) {
            return candidate;
        }
        var (year, month) = after.Month == 12 ? (after.Year + 1, 1) : (after.Year, after.Month + 1);
        return onAnchorDay(year, month);
    }

    private DateOnly onAnchorDay(int year, int month) {
        var day = Math.Min(AnchorDay, DateTime.DaysInMonth(year, month));
        return new DateOnly(year, month, day);
    }
}

namespace PersonalFinance.SharedKernel;

public static class DateOnlyExtensions {
    /// <summary>
    /// Calendar-month ordinal (<c>year * 12 + month</c>) for comparing months regardless of day.
    /// </summary>
    public static int MonthOrdinal(this DateOnly date) {
        return date.Year * 12 + date.Month;
    }
}

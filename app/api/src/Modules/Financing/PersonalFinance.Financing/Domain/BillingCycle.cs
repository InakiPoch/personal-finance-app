namespace PersonalFinance.Financing.Domain;

/// <summary>
/// The statement cycle a purchase closes into — identified by its cutoff month, not the calendar month of the purchase.
/// </summary>
internal sealed record BillingCycle(int Year, int Month) {
    public BillingCycle DueCycle => AddMonths(1);

    public BillingCycle AddMonths(int months) {
        var zeroBased = (Year * 12) + (Month - 1) + months;
        var (year, monthIndex) = Math.DivRem(zeroBased, 12);
        if(monthIndex >= 0) return new BillingCycle(year, monthIndex + 1);
        monthIndex += 12;
        year -= 1;
        return new BillingCycle(year, monthIndex + 1);
    }
    
    public bool IsClosedAsOf(DateOnly today, int cutoffDay) {
        var day = Math.Min(cutoffDay, DateTime.DaysInMonth(Year, Month));
        return today > new DateOnly(Year, Month, day);
    }
}

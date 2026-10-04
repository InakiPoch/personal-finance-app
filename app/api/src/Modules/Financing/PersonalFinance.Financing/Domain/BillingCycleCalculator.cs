namespace PersonalFinance.Financing.Domain;

/// <summary>
/// Assigns a purchase to its billing cycle: a purchase on the cutoff day or earlier
/// closes into the current cycle; a later purchase closes into the next one. The cutoff day is
/// clamped to the purchase month's length.
/// </summary>
internal static class BillingCycleCalculator {
    public static BillingCycle ResolveCycle(DateOnly purchaseDate, int cutoffDay) {
        var effectiveCutoff = Math.Min(cutoffDay, DateTime.DaysInMonth(purchaseDate.Year, purchaseDate.Month));
        var cycle = new BillingCycle(purchaseDate.Year, purchaseDate.Month);
        return purchaseDate.Day <= effectiveCutoff ? cycle : cycle.AddMonths(1);
    }
}

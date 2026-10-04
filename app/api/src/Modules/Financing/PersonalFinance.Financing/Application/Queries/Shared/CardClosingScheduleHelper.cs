using PersonalFinance.Financing.Domain;

namespace PersonalFinance.Financing.Application.Queries.Shared;

internal static class CardClosingScheduleHelper {
    /// <summary>
    /// The first billing month, counting from today's calendar month, that has no statement charged yet.
    /// </summary>
    public static BillingCycle FirstOpenCycle(DateOnly today, IReadOnlySet<(int Year, int Month)> lockedCycles) {
        var cycle = new BillingCycle(today.Year, today.Month);
        while(lockedCycles.Contains((cycle.Year, cycle.Month))) {
            cycle = cycle.AddMonths(1);
        }
        return cycle;
    }
}

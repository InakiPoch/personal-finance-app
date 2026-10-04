using PersonalFinance.Financing.Domain;

namespace PersonalFinance.Financing.Application.Queries.Shared;

/// <summary>
/// The single definition of "due now" for creditor-financed installments (cutoff-day based), shared by the
/// Owed to Creditors list and the dashboard "Due this month" query so they can never disagree.
/// </summary>
internal static class CreditorDueNowHelper {
    public static int CurrentDueOrdinal(DateOnly today) {
        var currentDueCycle = BillingCycleCalculator.ResolveCycle(today, PaymentPlan.CreditorCutoffDay).DueCycle;
        return currentDueCycle.Year * 12 + currentDueCycle.Month;
    }

    public static bool IsDueNow(int cycleYear, int cycleMonth, int currentDueOrdinal) {
        var dueCycle = new BillingCycle(cycleYear, cycleMonth).DueCycle;
        return dueCycle.Year * 12 + dueCycle.Month <= currentDueOrdinal;
    }

    public static long RemainingMinorUnits(long amountMinorUnits, long paidMinorUnits) {
        return amountMinorUnits - paidMinorUnits;
    }
}

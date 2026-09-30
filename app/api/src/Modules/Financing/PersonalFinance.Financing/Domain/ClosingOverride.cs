using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Domain;

/// <summary>
/// A card's closing day for one specific billing cycle, overriding its usual closing day.
/// </summary>
internal sealed class ClosingOverride : Entity<Guid> {
    public Guid CardId { get; }
    public int CycleYear { get; }
    public int CycleMonth { get; }
    public int ClosingDay { get; private set; }
    public BillingCycle Cycle => new(CycleYear, CycleMonth);

    private ClosingOverride(Guid id, Guid cardId, int cycleYear, int cycleMonth, int closingDay) : base(id) {
        CardId = cardId;
        CycleYear = cycleYear;
        CycleMonth = cycleMonth;
        ClosingDay = closingDay;
    }

    internal static ClosingOverride For(Guid cardId, BillingCycle cycle, int closingDay) {
        return new ClosingOverride(Guid.CreateVersion7(), cardId, cycle.Year, cycle.Month, closingDay);
    }

    internal void ChangeDay(int closingDay) {
        ClosingDay = closingDay;
    }
}

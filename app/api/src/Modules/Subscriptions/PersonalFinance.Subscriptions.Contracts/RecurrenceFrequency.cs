namespace PersonalFinance.Subscriptions.Contracts;

/// <summary>
/// How often a subscription renews. Monthly only for now — the seam stays open for
/// Weekly / Annually later.
/// </summary>
public enum RecurrenceFrequency {
    Monthly = 1
}

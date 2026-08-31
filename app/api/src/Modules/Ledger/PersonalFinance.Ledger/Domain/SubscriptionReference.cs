namespace PersonalFinance.Ledger.Domain;

/// <summary>
/// Opaque reference to a subscription owned by the Subscriptions context.
/// </summary>
internal sealed record SubscriptionReference(Guid Value);

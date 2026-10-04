namespace PersonalFinance.Ledger.Domain;

/// <summary>
/// Opaque reference to a shared-expense split owned by the Parties context.
/// </summary>
internal sealed record SplitReference(Guid Value);

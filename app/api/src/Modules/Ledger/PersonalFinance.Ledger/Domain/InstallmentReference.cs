namespace PersonalFinance.Ledger.Domain;

/// <summary>
/// Opaque reference to an installment owned by the Financing context.
/// </summary>
internal sealed record InstallmentReference(Guid Value);

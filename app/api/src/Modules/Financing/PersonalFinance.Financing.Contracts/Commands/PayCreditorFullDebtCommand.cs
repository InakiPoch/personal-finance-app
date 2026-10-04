using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Financing.Contracts.Commands;

/// <summary>
/// Settles a creditor's debt
/// </summary>
public sealed record PayCreditorFullDebtCommand(Guid CreditorId, long? AmountMinorUnits = null, string? CurrencyCode = null) : ICommand<int>;

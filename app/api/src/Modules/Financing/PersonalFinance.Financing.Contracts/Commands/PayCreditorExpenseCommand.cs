using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Financing.Contracts.Commands;

/// <summary>
/// Pays one creditor-financed purchase (full or partial) — fills that plan's installments in
/// <c>Sequence</c> order, each taken in full until the amount runs out, the last one getting the leftover as a partial.
/// </summary>
public sealed record PayCreditorExpenseCommand(Guid PaymentPlanId, long? AmountMinorUnits = null) : ICommand<int>;

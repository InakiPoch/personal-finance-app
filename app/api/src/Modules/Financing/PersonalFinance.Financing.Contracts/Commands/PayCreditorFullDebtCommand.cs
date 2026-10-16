using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Financing.Contracts.Commands;

/// <summary>
/// Settles a creditor's debt from the <see cref="BankAccountId"/> Bank/Cash account, one transaction per currency
/// </summary>
public sealed record PayCreditorFullDebtCommand(Guid CreditorId, Guid BankAccountId, long? AmountMinorUnits = null, string? CurrencyCode = null) : ICommand<int>;

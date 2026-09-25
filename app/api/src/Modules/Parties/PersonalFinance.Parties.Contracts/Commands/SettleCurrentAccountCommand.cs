using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Parties.Contracts.Commands;

/// <summary>
/// Records a payment received from a party against its outstanding balance.
/// Posts the receivable-cancellation entry (<c>Dr Bank / Cr Receivable</c> — explicitly not income) via
/// <c>ILedgerApi</c> and emits <see cref="IntegrationEvents.ExpenseSplitSettledIntegrationEvent"/>.
/// </summary>
public sealed record SettleCurrentAccountCommand(
    Guid PartyId,
    long AmountMinorUnits,
    Guid BankAccountId,
    DateTimeOffset SettledOnUtc,
    string CurrencyCode = "ARS"
) : ICommand<Guid>;

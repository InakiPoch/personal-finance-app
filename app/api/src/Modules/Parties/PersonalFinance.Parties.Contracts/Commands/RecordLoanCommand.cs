using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Parties.Contracts.Commands;

/// <summary>
/// Records money lent to a party from a Bank/Cash account: <c>Dr Receivable_party / Cr Bank|Cash</c> via <c>ILedgerApi</c>.
/// </summary>
public sealed record RecordLoanCommand(
    Guid PartyId,
    long AmountMinorUnits,
    Guid SourceAccountId,
    DateOnly LentOn,
    string Description,
    string CurrencyCode = "ARS"
) : ICommand<Guid>;

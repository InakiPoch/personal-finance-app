using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Parties.Contracts.Commands;

/// <summary>
/// Records paying back a party from a Bank/Cash account: <c>Dr Payable_party / Cr Bank|Cash</c>.
/// </summary>
public sealed record RepayPartyCommand(
    Guid PartyId,
    long AmountMinorUnits,
    Guid SourceAccountId,
    DateOnly PaidOn,
    string CurrencyCode = "ARS",
    DateOnly? Today = null
) : ICommand<Guid>;

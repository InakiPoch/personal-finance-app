using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Parties.Contracts.Commands;

/// <summary>
/// Records money borrowed from a party into a Bank/Cash account: <c>Dr Bank|Cash / Cr Payable_party</c>.
/// </summary>
public sealed record RecordBorrowingCommand(
    Guid PartyId,
    long AmountMinorUnits,
    Guid DestinationAccountId,
    DateOnly BorrowedOn,
    string Description,
    string CurrencyCode = "ARS",
    DateOnly? Today = null
) : ICommand<Guid>;

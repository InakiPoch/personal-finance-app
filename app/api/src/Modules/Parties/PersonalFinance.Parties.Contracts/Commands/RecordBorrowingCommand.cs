using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Parties.Contracts.Commands;

/// <summary>
/// Records money borrowed from a party into a Bank/Cash account: <c>Dr Bank|Cash / Cr Payable_party</c>.
/// <c>Today</c> is the caller's local date used for the "not in the future" check (falls back to UTC today).
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

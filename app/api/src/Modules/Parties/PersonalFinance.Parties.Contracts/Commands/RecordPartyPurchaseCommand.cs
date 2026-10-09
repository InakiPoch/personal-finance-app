using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Parties.Contracts.Commands;

/// <summary>
/// Records a purchase a party paid where I took part; returns the purchase id. Kind <c>debit</c> posts
/// <c>Dr Expense(category) / Cr Payable_party</c> on the purchase date. <c>Today</c> is the caller's local date
/// used for the "not in the future" check (falls back to UTC today).
/// </summary>
public sealed record RecordPartyPurchaseCommand(
    Guid PartyId,
    long ShareMinorUnits,
    string CurrencyCode,
    string Description,
    string CategoryName,
    DateOnly PurchaseDate,
    string Kind,
    DateOnly? Today = null
) : ICommand<Guid>;

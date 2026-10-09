using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Parties.Contracts.Commands;

/// <summary>
/// Records a purchase a party paid where I took part; returns the purchase id.
/// </summary>
public sealed record RecordPartyPurchaseCommand(
    Guid PartyId,
    long ShareMinorUnits,
    string CurrencyCode,
    string Description,
    string CategoryName,
    DateOnly PurchaseDate,
    string Kind,
    DateOnly? Today = null,
    int InstallmentCount = 1,
    DateOnly? FirstPaymentMonth = null
) : ICommand<Guid>;

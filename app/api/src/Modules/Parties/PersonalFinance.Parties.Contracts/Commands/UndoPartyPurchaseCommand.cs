using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Parties.Contracts.Commands;

/// <summary>
/// Undoes a party purchase as a whole by reversing every ledger transaction it posted.
/// </summary>
public sealed record UndoPartyPurchaseCommand(Guid PartyId, Guid PurchaseId) : ICommand;

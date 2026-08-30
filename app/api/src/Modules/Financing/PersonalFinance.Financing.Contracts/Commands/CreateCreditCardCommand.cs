using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Financing.Contracts.Commands;

/// <summary>
/// Registers a credit card with its statement cutoff day. The handler provisions the
/// card's dedicated ledger accounts (liability + purchases) through <c>ILedgerApi</c>.
/// </summary>
public sealed record CreateCreditCardCommand(string Name, int CutoffDate) : ICommand<Guid>;

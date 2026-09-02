using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Ledger.Contracts.Commands;

/// <summary>
/// Creates a ledger account.
/// </summary>
public sealed record CreateAccountCommand(string Name, AccountType Type, AccountKind Kind, Guid? OwnerReferenceId = null) : ICommand<Guid>;

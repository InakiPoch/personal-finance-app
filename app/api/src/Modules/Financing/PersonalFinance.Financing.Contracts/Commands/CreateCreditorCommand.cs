using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Financing.Contracts.Commands;

public sealed record CreditorAccountPayload(string Label, string? Identifier);

/// <summary>
/// Registers a creditor together with its accounts in one submit.
/// </summary>
public sealed record CreateCreditorCommand(string Name, IReadOnlyList<CreditorAccountPayload> Accounts) : ICommand<Guid>;

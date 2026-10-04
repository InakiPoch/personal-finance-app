using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Parties.Contracts.Commands;

public sealed record CreatePartyCommand(string Name) : ICommand<Guid>;

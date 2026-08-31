using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Parties.Contracts.Commands;

public sealed record SharedExpenseParticipant(Guid PartyId, long Weight);

public sealed record RegisterSharedExpenseCommand(
    string Description,
    long TotalMinorUnits,
    Guid ExpenseAccountId,
    Guid FundingAccountId,
    DateTimeOffset IncurredOnUtc,
    IReadOnlyList<SharedExpenseParticipant> Participants
) : ICommand<Guid>;

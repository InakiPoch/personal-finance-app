namespace PersonalFinance.Api.Endpoints.DTOs;

public sealed record SharedExpenseParticipantDto(Guid PartyId, long Weight);

public sealed record RegisterSharedExpenseDto(
    string Description,
    long TotalMinorUnits,
    Guid ExpenseAccountId,
    Guid FundingAccountId,
    DateTimeOffset IncurredOnUtc,
    IReadOnlyList<SharedExpenseParticipantDto> Participants
);

public sealed record SharedExpenseResultDto(Guid SplitReferenceId);

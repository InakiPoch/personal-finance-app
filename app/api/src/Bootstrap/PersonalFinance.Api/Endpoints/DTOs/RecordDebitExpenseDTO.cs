namespace PersonalFinance.Api.Endpoints.DTOs;

public sealed record DebitExpenseParticipantDto(Guid PartyId, long Weight);

public sealed record RecordDebitExpenseDto(
    long AmountMinorUnits,
    Guid SourceInstrumentId,
    string CategoryName,
    string PurchaseDate,
    string Description,
    IReadOnlyList<DebitExpenseParticipantDto>? Split = null
);

public sealed record RecordDebitExpenseResultDto(Guid Id);

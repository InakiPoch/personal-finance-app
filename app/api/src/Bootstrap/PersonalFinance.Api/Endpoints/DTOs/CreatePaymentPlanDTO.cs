namespace PersonalFinance.Api.Endpoints.DTOs;

public sealed record SplitParticipantDto(Guid PartyId, long Weight);

public sealed record CreatePaymentPlanDto(
    long AmountMinorUnits, 
    Guid CardId, 
    int InstallmentCount, 
    string PurchaseDate,
    IReadOnlyList<SplitParticipantDto>? Split = null,
    Guid? CreditorId = null, 
    Guid? CreditorAccountId = null
);

public sealed record CreatePaymentPlanResultDto(Guid PaymentPlanId);

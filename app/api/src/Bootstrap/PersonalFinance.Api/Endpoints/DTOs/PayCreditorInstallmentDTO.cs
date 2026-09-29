namespace PersonalFinance.Api.Endpoints.DTOs;

/// <summary>
/// A null <see cref="AmountMinorUnits"/> means "pay whatever remains" (a full payment).
/// </summary>
public sealed record PayCreditorInstallmentRequestDto(long? AmountMinorUnits);

public sealed record PayCreditorInstallmentResultDto(Guid InstallmentId);

public sealed record PayCreditorInstallmentPartyShareRequestDto(Guid PartyId, Guid BankAccountId);

public sealed record PayCreditorInstallmentPartyShareResultDto(Guid PaymentId);

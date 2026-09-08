namespace PersonalFinance.Api.Endpoints.DTOs;

public sealed record CreditorPayableAccountDto(Guid AccountId, string Label, long OutstandingMinorUnits);

public sealed record CreditorPayableRowDto(
    Guid CreditorId,
    string CreditorName,
    long DueNowMinorUnits,
    long TotalOwedMinorUnits,
    DateOnly? NextDueDate,
    IReadOnlyList<CreditorPayableAccountDto> Accounts
);

public sealed record CreditorPayablesDto(IReadOnlyList<CreditorPayableRowDto> Rows);

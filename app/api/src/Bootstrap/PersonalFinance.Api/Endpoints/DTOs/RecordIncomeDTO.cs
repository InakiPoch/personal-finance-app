namespace PersonalFinance.Api.Endpoints.DTOs;

public sealed record RecordIncomeDto(
    long AmountMinorUnits,
    Guid TargetAccountId,
    string ReceivedOn,
    string Description,
    string CurrencyCode = "ARS"
);

public sealed record RecordIncomeResultDto(Guid Id);

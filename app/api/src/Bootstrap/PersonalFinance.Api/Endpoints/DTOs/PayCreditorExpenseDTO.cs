namespace PersonalFinance.Api.Endpoints.DTOs;

/// <summary>
/// A null <see cref="AmountMinorUnits"/> means "pay whatever remains" on the whole purchase.
/// </summary>
public sealed record PayCreditorExpenseRequestDto(Guid BankAccountId, long? AmountMinorUnits);

public sealed record PayCreditorExpenseResultDto(int SettledCount);

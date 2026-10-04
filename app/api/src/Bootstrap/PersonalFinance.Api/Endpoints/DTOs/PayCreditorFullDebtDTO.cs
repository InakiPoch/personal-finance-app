namespace PersonalFinance.Api.Endpoints.DTOs;

/// <summary>
/// A null <see cref="AmountMinorUnits"/> settles the creditor's entire debt in every currency.
/// A set amount requires <see cref="CurrencyCode"/> and only settles that currency's remaining debt.
/// </summary>
public sealed record PayCreditorFullDebtRequestDto(long? AmountMinorUnits, string? CurrencyCode);

public sealed record PayCreditorFullDebtResultDto(int SettledCount);

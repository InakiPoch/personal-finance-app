namespace PersonalFinance.Api.Endpoints.DTOs;

/// <summary>
/// The installment id comes from the route; the body carries only the funding account and the pay date.
/// </summary>
public sealed record PayInstallmentDto(Guid BankAccountId, DateTimeOffset PaidOnUtc);

public sealed record PayInstallmentResultDto(Guid InstallmentId);

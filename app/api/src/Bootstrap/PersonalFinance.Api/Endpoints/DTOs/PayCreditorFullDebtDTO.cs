namespace PersonalFinance.Api.Endpoints.DTOs;

/// <summary>
/// Paying a creditor's full debt takes no input — the creditor id comes from the route and the paid
/// timestamp is the server clock. The result carries the number of installments newly settled.
/// </summary>
public sealed record PayCreditorFullDebtResultDto(int SettledCount);

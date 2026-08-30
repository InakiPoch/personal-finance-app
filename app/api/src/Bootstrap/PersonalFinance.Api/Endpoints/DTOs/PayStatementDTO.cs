namespace PersonalFinance.Api.Endpoints.DTOs;

/// <summary>
/// The statement id comes from the route; the body carries only the funding account and the pay date.
/// </summary>
public sealed record PayStatementDto(Guid BankAccountId, DateTimeOffset PaidOnUtc);

public sealed record PayStatementResultDto(Guid StatementId);

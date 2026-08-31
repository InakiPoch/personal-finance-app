namespace PersonalFinance.Api.Endpoints.DTOs;

public sealed record CreateSubscriptionDto(string Name, long AmountMinorUnits, string Category, Guid FundingAccountId, string Frequency, int AnchorDay);

namespace PersonalFinance.Api.Endpoints.DTOs;

// The party id comes from the route; the body carries the payment amount, the bank account, and the date.
public sealed record SettleCurrentAccountDto(long AmountMinorUnits, Guid BankAccountId, DateTimeOffset SettledOnUtc);

public sealed record SettlementResultDto(Guid LedgerTransactionId);

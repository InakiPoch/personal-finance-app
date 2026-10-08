namespace PersonalFinance.Api.Endpoints.DTOs;

// The party id comes from the route; the body carries the payment amount, the bank account, and the date.
public sealed record SettleCurrentAccountDto(long AmountMinorUnits, Guid BankAccountId, DateTimeOffset SettledOnUtc, string CurrencyCode = "ARS");

// The party id comes from the route; the body carries the loan amount, the Bank/Cash source account, the "Lent on" date and a description.
public sealed record RecordLoanDto(long AmountMinorUnits, Guid SourceAccountId, DateOnly LentOn, string Description, string CurrencyCode = "ARS");

public sealed record LoanResultDto(Guid LedgerTransactionId);

public sealed record SettlementResultDto(Guid LedgerTransactionId);

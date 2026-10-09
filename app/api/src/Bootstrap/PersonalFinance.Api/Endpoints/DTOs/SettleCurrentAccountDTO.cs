namespace PersonalFinance.Api.Endpoints.DTOs;

public sealed record SettleCurrentAccountDto(long AmountMinorUnits, Guid BankAccountId, DateTimeOffset SettledOnUtc, string CurrencyCode = "ARS");

public sealed record RecordLoanDto(long AmountMinorUnits, Guid SourceAccountId, DateOnly LentOn, string Description, string CurrencyCode = "ARS", DateOnly? Today = null);

public sealed record RecordBorrowingDto(long AmountMinorUnits, Guid DestinationAccountId, DateOnly BorrowedOn, string Description, string CurrencyCode = "ARS", DateOnly? Today = null);

public sealed record RecordPartyPurchaseDto(long ShareMinorUnits, string CurrencyCode, string Description, string CategoryName, DateOnly PurchaseDate, string Kind, DateOnly? Today = null, int InstallmentCount = 1, DateOnly? FirstPaymentMonth = null);

public sealed record PartyPurchaseResultDto(Guid PurchaseId);

public sealed record LoanResultDto(Guid LedgerTransactionId);

public sealed record BorrowingResultDto(Guid LedgerTransactionId);

public sealed record RepayPartyDto(long AmountMinorUnits, Guid SourceAccountId, DateOnly PaidOn, string CurrencyCode = "ARS", DateOnly? Today = null);

public sealed record RepaymentResultDto(Guid LedgerTransactionId);

public sealed record SettlementResultDto(Guid LedgerTransactionId);

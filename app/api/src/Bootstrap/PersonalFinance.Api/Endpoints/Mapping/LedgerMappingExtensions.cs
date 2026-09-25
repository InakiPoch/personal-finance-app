using System.Globalization;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Ledger.Contracts.Queries;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Api.Endpoints.Mapping;

/// <summary>
/// DTO ⇄ Ledger contract mapping. Currency is <see cref="Currency.Reference"/>
/// </summary>
internal static class LedgerMappingExtensions {
    public static PostTransactionCommand ToPostTransactionCommand(this PostTransactionDto dto) {
        var lines = dto.Lines
            .Select(line => new PostTransactionLine(
                line.AccountId,
                Enum.Parse<DebitOrCredit>(line.Direction, ignoreCase: true),
                Money.FromMinorUnits(line.AmountMinorUnits, Currency.Reference)))
            .ToList();
        return new PostTransactionCommand(
            lines,
            dto.PostedOnUtc,
            dto.SplitReferenceId,
            dto.InstallmentReferenceId,
            Description: dto.Description
        );
    }

    public static CreateAccountCommand ToCreateAccountCommand(this CreateAccountDto dto) {
        return new CreateAccountCommand(
            dto.Name,
            Enum.Parse<AccountType>(dto.Type, ignoreCase: true),
            Enum.Parse<AccountKind>(dto.Kind, ignoreCase: true)
        );
    }

    public static RecordDebitExpenseCommand ToRecordDebitExpenseCommand(this RecordDebitExpenseDto dto) {
        var split = dto.Split?
            .Select(participant => new RecordDebitExpenseParticipant(participant.PartyId, participant.Weight))
            .ToList();
        return new RecordDebitExpenseCommand(
            dto.AmountMinorUnits,
            dto.SourceInstrumentId,
            dto.CategoryName,
            DateOnly.Parse(dto.PurchaseDate, CultureInfo.InvariantCulture),
            dto.Description,
            split,
            dto.CurrencyCode
        );
    }

    public static PostTransactionResultDto ToPostTransactionResultDto(this Guid transactionId) {
        return new PostTransactionResultDto(transactionId);
    }

    public static RecordDebitExpenseResultDto ToRecordDebitExpenseResultDto(this Guid id) {
        return new RecordDebitExpenseResultDto(id);
    }

    public static RecordIncomeCommand ToRecordIncomeCommand(this RecordIncomeDto dto) {
        return new RecordIncomeCommand(
            dto.AmountMinorUnits,
            dto.TargetAccountId,
            DateOnly.Parse(dto.ReceivedOn, CultureInfo.InvariantCulture),
            dto.Description,
            dto.CurrencyCode
        );
    }

    public static RecordIncomeResultDto ToRecordIncomeResultDto(this Guid id) {
        return new RecordIncomeResultDto(id);
    }
    
    public static ReverseTransactionResultDto ToReverseTransactionResultDto(this ReverseTransactionResult result, Guid originalTransactionId) {
        return new ReverseTransactionResultDto(result.ReversalTransactionId, originalTransactionId, result.CompensatingEntryPosted);
    }
    
    public static CreateAccountResultDto ToCreateAccountResultDto(this Guid accountId) {
        return new CreateAccountResultDto(accountId);
    }

    public static AccountBalanceDto ToAccountBalanceDto(this IReadOnlyList<Money> balances, Guid accountId) {
        var rows = balances
            .Select(balance => new AccountBalanceRowDto(balance.MinorUnits, balance.Currency.Code, balance.ToString()))
            .ToList();
        return new AccountBalanceDto(accountId, rows);
    }

    public static GetTransactionsQuery ToGetTransactionsQuery(this Guid? accountId, string? from, string? to) {
        return new GetTransactionsQuery(accountId, parseDateOnly(from), parseDateOnly(to));
    }

    public static TransactionFeedDto ToTransactionFeedDto(this TransactionFeedResponse response) {
        var rows = response.Rows
            .Select(row => new TransactionFeedRowDto(
                row.TransactionId,
                row.PostedOnUtc,
                row.Description,
                row.AmountMinorUnits,
                row.CurrencyCode,
                row.IsReversal,
                row.IsReversed,
                row.InstallmentReferenceId,
                row.SplitReferenceId))
            .ToList();
        return new TransactionFeedDto(rows);
    }

    private static DateOnly? parseDateOnly(string? value) {
        return string.IsNullOrWhiteSpace(value) ? null : DateOnly.Parse(value, CultureInfo.InvariantCulture);
    }
}

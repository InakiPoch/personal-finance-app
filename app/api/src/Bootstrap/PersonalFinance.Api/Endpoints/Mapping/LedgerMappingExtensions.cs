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
    public static PostTransactionResultDto ToPostTransactionResultDto(this Guid transactionId) {
        return new PostTransactionResultDto(transactionId);
    }
    
    public static ReverseTransactionResultDto ToReverseTransactionResultDto(this ReverseTransactionResult result, Guid originalTransactionId) {
        return new ReverseTransactionResultDto(result.ReversalTransactionId, originalTransactionId, result.CompensatingEntryPosted);
    }
    
    public static CreateAccountResultDto ToCreateAccountResultDto(this Guid accountId) {
        return new CreateAccountResultDto(accountId);
    }

    public static AccountBalanceDto ToAccountBalanceDto(this Money balance, Guid accountId) {
        return new AccountBalanceDto(accountId, balance.MinorUnits, balance.Currency.Code, balance.ToString());
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

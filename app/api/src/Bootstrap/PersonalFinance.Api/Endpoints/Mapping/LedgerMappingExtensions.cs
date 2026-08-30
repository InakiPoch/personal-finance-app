using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
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
            dto.Description
        );
    }

    public static CreateAccountCommand ToCreateAccountCommand(this CreateAccountDto dto) {
        return new CreateAccountCommand(
            dto.Name,
            Enum.Parse<AccountType>(dto.Type, ignoreCase: true),
            Enum.Parse<AccountKind>(dto.Kind, ignoreCase: true)
        );
    }

    extension(Guid transactionId) {
        public PostTransactionResultDto ToPostTransactionResultDto() {
            return new PostTransactionResultDto(transactionId);
        }

        public ReverseTransactionResultDto ToReverseTransactionResultDto(Guid originalTransactionId) {
            return new ReverseTransactionResultDto(transactionId, originalTransactionId, CompensatingEntryPosted: false);
        }
    }
    
    public static CreateAccountResultDto ToCreateAccountResultDto(this Guid accountId) {
        return new CreateAccountResultDto(accountId);
    }

    public static AccountBalanceDto ToAccountBalanceDto(this Money balance, Guid accountId) {
        return new AccountBalanceDto(accountId, balance.MinorUnits, balance.Currency.Code, balance.ToString());
    }
}

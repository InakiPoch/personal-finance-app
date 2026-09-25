using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Ledger.Domain;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Ledger.Application.Commands.RecordIncome;

internal static class RecordIncomeValidator {
    public static Result Validate(RecordIncomeCommand command) {
        if(command.AmountMinorUnits <= 0) {
            return Result.Failure(LedgerErrors.NonPositiveEntryAmount);
        }
        if(string.IsNullOrWhiteSpace(command.Description)) {
            return Result.Failure(LedgerErrors.InvalidIncomeDescription);
        }
        if(command.TargetAccountId == Guid.Empty) {
            return Result.Failure(LedgerErrors.AccountNotFound);
        }
        if(command.CurrencyCode is not ("ARS" or "USD")) {
            return Result.Failure(LedgerErrors.InvalidCurrencyCode);
        }
        return Result.Success();
    }
}

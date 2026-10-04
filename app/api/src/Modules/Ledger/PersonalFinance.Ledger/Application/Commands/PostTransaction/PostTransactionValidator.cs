using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Ledger.Domain;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Ledger.Application.Commands.PostTransaction;

internal static class PostTransactionValidator {
    public static Result Validate(PostTransactionCommand command) {
        if(command.Lines.Count < 2) {
            return Result.Failure(LedgerErrors.DegenerateTransaction);
        }
        if(command.Lines.Any(line => line.AccountId == Guid.Empty)) {
            return Result.Failure(LedgerErrors.AccountNotFound);
        }
        if(command.Lines.Any(line => line.Amount.MinorUnits <= 0)) {
            return Result.Failure(LedgerErrors.NonPositiveEntryAmount);
        }
        var currency = command.Lines[0].Amount.Currency;
        return command.Lines.Any(line => line.Amount.Currency != currency) ? Result.Failure(LedgerErrors.MixedCurrency) : Result.Success();
    }
}

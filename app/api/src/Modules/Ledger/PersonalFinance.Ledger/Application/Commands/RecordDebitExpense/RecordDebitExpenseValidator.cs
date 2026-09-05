using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Ledger.Domain;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Ledger.Application.Commands.RecordDebitExpense;

internal static class RecordDebitExpenseValidator {
    public static Result Validate(RecordDebitExpenseCommand command) {
        if(command.AmountMinorUnits <= 0) {
            return Result.Failure(LedgerErrors.NonPositiveEntryAmount);
        }
        if(string.IsNullOrWhiteSpace(command.CategoryName)) {
            return Result.Failure(LedgerErrors.InvalidExpenseCategory);
        }
        if(string.IsNullOrWhiteSpace(command.Description)) {
            return Result.Failure(LedgerErrors.InvalidExpenseDescription);
        }
        if(command.SourceAccountId == Guid.Empty) {
            return Result.Failure(LedgerErrors.AccountNotFound);
        }
        if(command.Split is null) {
            return Result.Success();
        }
        if(command.Split.Count == 0) {
            return Result.Failure(LedgerErrors.InvalidExpenseSplit);
        }
        var seen = new HashSet<Guid>();
        foreach(var participant in command.Split) {
            if(participant.PartyId == Guid.Empty || participant.Weight <= 0 || !seen.Add(participant.PartyId)) {
                return Result.Failure(LedgerErrors.InvalidExpenseSplit);
            }
        }
        return Result.Success();
    }
}

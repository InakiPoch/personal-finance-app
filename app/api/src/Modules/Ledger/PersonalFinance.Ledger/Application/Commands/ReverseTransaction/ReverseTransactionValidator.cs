using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Ledger.Domain;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Ledger.Application.Commands.ReverseTransaction;

internal static class ReverseTransactionValidator {
    public static Result Validate(ReverseTransactionCommand command) {
        return command.OriginalTransactionId == Guid.Empty
            ? Result.Failure(LedgerErrors.OriginalTransactionNotFound)
            : Result.Success();
    }
}

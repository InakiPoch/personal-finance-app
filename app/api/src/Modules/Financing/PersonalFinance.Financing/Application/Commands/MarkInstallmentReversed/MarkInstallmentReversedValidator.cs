using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Domain;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Application.Commands.MarkInstallmentReversed;

internal static class MarkInstallmentReversedValidator {
    public static Result Validate(MarkInstallmentReversedCommand command) {
        if(command.InstallmentId == Guid.Empty) {
            return Result.Failure(FinancingErrors.InstallmentNotFound);
        }
        return command is { CompensatingCreditPosted: true, CreditAmountMinorUnits: <= 0 } ? Result.Failure(FinancingErrors.NonPositiveCreditAmount) : Result.Success();
    }
}

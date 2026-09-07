using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Domain;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Application.Commands.PayInstallment;

internal static class PayInstallmentValidator {
    public static Result Validate(PayInstallmentCommand command) {
        if(command.InstallmentId == Guid.Empty) {
            return Result.Failure(FinancingErrors.InstallmentNotFound);
        }
        return command.BankAccountId == Guid.Empty ? Result.Failure(FinancingErrors.InvalidBankAccount) : Result.Success();
    }
}

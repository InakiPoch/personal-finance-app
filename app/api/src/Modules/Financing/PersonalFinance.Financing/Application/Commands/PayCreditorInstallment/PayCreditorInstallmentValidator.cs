using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Domain;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Application.Commands.PayCreditorInstallment;

internal static class PayCreditorInstallmentValidator {
    public static Result Validate(PayCreditorInstallmentCommand command) {
        return command.InstallmentId == Guid.Empty ? Result.Failure(FinancingErrors.InstallmentNotFound) : Result.Success();
    }
}

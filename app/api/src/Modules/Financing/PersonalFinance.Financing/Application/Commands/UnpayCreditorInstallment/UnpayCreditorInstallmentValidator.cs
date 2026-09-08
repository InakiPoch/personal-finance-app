using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Domain;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Application.Commands.UnpayCreditorInstallment;

internal static class UnpayCreditorInstallmentValidator {
    public static Result Validate(UnpayCreditorInstallmentCommand command) {
        return command.InstallmentId == Guid.Empty ? Result.Failure(FinancingErrors.InstallmentNotFound) : Result.Success();
    }
}

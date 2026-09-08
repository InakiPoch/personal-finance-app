using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Domain;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Application.Commands.PayCreditorFullDebt;

internal static class PayCreditorFullDebtValidator {
    public static Result Validate(PayCreditorFullDebtCommand command) {
        return command.CreditorId == Guid.Empty ? Result.Failure(FinancingErrors.CreditorNotFound) : Result.Success();
    }
}

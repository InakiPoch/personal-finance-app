using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Domain;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Application.Commands.PayCreditorExpense;

internal static class PayCreditorExpenseValidator {
    public static Result Validate(PayCreditorExpenseCommand command) {
        if(command.BankAccountId == Guid.Empty) {
            return Result.Failure(FinancingErrors.InvalidPaidFromAccount);
        }
        return command.PaymentPlanId == Guid.Empty ? Result.Failure(FinancingErrors.PaymentPlanNotFound) : Result.Success();
    }
}

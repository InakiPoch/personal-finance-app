using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Domain;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Application.Commands.PayStatement;

internal static class PayStatementValidator {
    public static Result Validate(PayStatementCommand command) {
        if(command.StatementId == Guid.Empty) {
            return Result.Failure(FinancingErrors.StatementNotFound);
        }
        return command.BankAccountId == Guid.Empty ? Result.Failure(FinancingErrors.InvalidBankAccount) : Result.Success();
    }
}

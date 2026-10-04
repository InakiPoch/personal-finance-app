using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Domain;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Application.Commands.PayCreditorFullDebt;

internal static class PayCreditorFullDebtValidator {
    public static Result Validate(PayCreditorFullDebtCommand command) {
        if(command.CreditorId == Guid.Empty) {
            return Result.Failure(FinancingErrors.CreditorNotFound);
        }
        if(command.AmountMinorUnits is not null && command.CurrencyCode is not ("ARS" or "USD")) {
            return Result.Failure(FinancingErrors.InvalidCurrencyCode);
        }
        return Result.Success();
    }
}

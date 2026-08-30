using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Domain;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Application.Commands.CreateCreditCard;

internal static class CreateCreditCardValidator {
    public static Result Validate(CreateCreditCardCommand command) {
        if(string.IsNullOrWhiteSpace(command.Name)) {
            return Result.Failure(FinancingErrors.InvalidCardName);
        }
        return command.CutoffDate is < 1 or > 31 ? Result.Failure(FinancingErrors.InvalidCutoffDay) : Result.Success();
    }
}

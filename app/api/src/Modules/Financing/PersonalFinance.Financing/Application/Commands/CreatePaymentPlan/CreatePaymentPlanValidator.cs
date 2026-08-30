using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Domain.Rules;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Application.Commands.CreatePaymentPlan;

internal static class CreatePaymentPlanValidator {
    public static Result Validate(CreatePaymentPlanCommand command) {
        if(command.AmountMinorUnits <= 0) {
            return Result.Failure(FinancingErrors.NonPositivePlanAmount);
        }
        if(command.InstallmentCount < 1) {
            return Result.Failure(FinancingErrors.InvalidInstallmentCount);
        }
        return command.Split is null ? Result.Success() : SplitWeightsMustBePositive.Check(command.Split.Participants);
    }
}

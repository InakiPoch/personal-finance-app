using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Domain.Rules;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Application.Commands.CreatePaymentPlan;

internal static class CreatePaymentPlanValidator {
    public static Result Validate(CreatePaymentPlanCommand command, DateOnly today) {
        if(command.AmountMinorUnits <= 0) {
            return Result.Failure(FinancingErrors.NonPositivePlanAmount);
        }
        if(command.InstallmentCount < 1) {
            return Result.Failure(FinancingErrors.InvalidInstallmentCount);
        }
        if(command.PurchaseDate > today) {
            return Result.Failure(FinancingErrors.FuturePurchaseDate);
        }
        if(command.CurrencyCode is not ("ARS" or "USD")) {
            return Result.Failure(FinancingErrors.InvalidCurrencyCode);
        }
        if(command.CardId is null && command.CreditorId is null) {
            return Result.Failure(FinancingErrors.PlanNeedsCardOrCreditor);
        }
        if(command.CardId is not null && command.CreditorId is not null) {
            return Result.Failure(FinancingErrors.PlanCannotMixCardAndCreditor);
        }
        if(command.CardId is null && command.CreditorAccountId is null) {
            return Result.Failure(FinancingErrors.CreditorAccountRequired);
        }
        var trimmedDescription = command.Description.Trim();
        switch(trimmedDescription.Length) {
            case < 1:
                return Result.Failure(FinancingErrors.BlankDescription);
            case > 120:
                return Result.Failure(FinancingErrors.DescriptionTooLong);
        }
        if(trimmedDescription.Contains('\n') || trimmedDescription.Contains('\r')) {
            return Result.Failure(FinancingErrors.DescriptionMustBeSingleLine);
        }
        return command.Split is null ? Result.Success() : SplitWeightsMustBePositive.Check(command.Split.Participants);
    }
}

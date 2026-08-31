using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Domain;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Application.Commands.LinkPaymentPlanSplit;

internal static class LinkPaymentPlanSplitValidator {
    public static Result Validate(LinkPaymentPlanSplitCommand command) {
        if(command.PaymentPlanId == Guid.Empty) {
            return Result.Failure(FinancingErrors.PaymentPlanNotFound);
        }
        if(command.SplitReferenceId == Guid.Empty || command.PartyReceivables is not { Count: > 0 }) {
            return Result.Failure(FinancingErrors.InvalidSplitReference);
        }
        foreach(var receivable in command.PartyReceivables) {
            if(receivable.PartyId == Guid.Empty || receivable.ReceivableAccountId == Guid.Empty) {
                return Result.Failure(FinancingErrors.InvalidSplitReference);
            }
        }
        return Result.Success();
    }
}

using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Domain.Rules;

internal static class SplitWeightsMustBePositive {
    public static Result Check(IReadOnlyList<SplitParticipant> participants) {
        if(participants.Count == 0 || participants.Any(participant => participant.Weight <= 0)) {
            return Result.Failure(FinancingErrors.InvalidSplitWeights);
        }
        return Result.Success();
    }
}

using PersonalFinance.SharedKernel.Allocation;

namespace PersonalFinance.Parties.Application;

/// <summary>
/// One shared-expense allocation: the holder's own slice and each participant's slice, in participant order.
/// </summary>
internal readonly record struct SplitShares(long HolderShare, IReadOnlyList<long> ParticipantShares);

internal static class SplitAllocationCalculator {
    public static SplitShares AllocateWhole(long total, IReadOnlyList<long> participantWeights) {
        guard(total, participantWeights);
        return split(total, participantWeights);
    }
    
    public static IReadOnlyList<SplitShares> AllocatePerInstallment(
        IReadOnlyList<long> installmentAmounts, IReadOnlyList<long> participantWeights) {
        ArgumentNullException.ThrowIfNull(installmentAmounts);
        if(installmentAmounts.Count == 0) {
            throw new ArgumentException("At least one installment is required.", nameof(installmentAmounts));
        }
        var result = new SplitShares[installmentAmounts.Count];
        for(var i = 0; i < installmentAmounts.Count; i++) {
            guard(installmentAmounts[i], participantWeights);
            result[i] = split(installmentAmounts[i], participantWeights);
        }
        return result;
    }

    private static SplitShares split(long amount, IReadOnlyList<long> participantWeights) {
        long[] weights = [1L, .. participantWeights];
        var shares = new PhantomPennyAllocator().Allocate(amount, weights);
        return new SplitShares(shares[0], [.. shares.Skip(1)]);
    }

    private static void guard(long amount, IReadOnlyList<long> participantWeights) {
        ArgumentNullException.ThrowIfNull(participantWeights);
        if(amount <= 0) {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "A shared amount must be positive.");
        }
        if(participantWeights.Count == 0) {
            throw new ArgumentException("A shared expense needs at least one participant.", nameof(participantWeights));
        }
    }
}

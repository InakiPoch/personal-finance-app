namespace PersonalFinance.SharedKernel.Allocation;

/// <summary>
/// Each part gets <c>total * weightᵢ / Σweight</c>
/// rounded toward zero; the leftover minor units are then handed out one at a time to the
/// parts with the largest fractional remainder, ties broken by lowest index.
/// </summary>
public sealed class PhantomPennyAllocator : IAllocationStrategy {
    public IReadOnlyList<long> Allocate(long total, IReadOnlyList<long> weights) {
        ArgumentNullException.ThrowIfNull(weights);
        if(weights.Count == 0) {
            throw new ArgumentException("At least one weight is required.", nameof(weights));
        }
        var weightSum = 0L;
        foreach(var weight in weights) {
            if(weight < 0) {
                throw new ArgumentException("Weights must be non-negative.", nameof(weights));
            }

            weightSum += weight;
        }
        if(weightSum == 0) {
            throw new ArgumentException("Weights must sum to a positive value.", nameof(weights));
        }
        var shares = new long[weights.Count];
        var remainders = new long[weights.Count];
        var distributed = 0L;
        for(var i = 0; i < weights.Count; i++) {
            var numerator = checked(total * weights[i]);
            var share = numerator / weightSum;
            shares[i] = share;
            remainders[i] = numerator - share * weightSum;
            distributed += share;
        }
        var leftover = total - distributed;
        var step = leftover < 0 ? -1L : 1L;
        var units = (int)Math.Abs(leftover);
        var order = Enumerable.Range(0, weights.Count)
            .OrderByDescending(i => step * remainders[i])
            .ThenBy(i => i)
            .ToArray();
        for(var k = 0; k < units; k++) {
            shares[order[k]] += step;
        }
        return shares;
    }

    public IReadOnlyList<Money> Allocate(Money total, IReadOnlyList<long> weights) {
        var parts = Allocate(total.MinorUnits, weights);
        var result = new Money[parts.Count];
        for(var i = 0; i < parts.Count; i++) {
            result[i] = new Money(parts[i], total.Currency);
        }
        return result;
    }
}

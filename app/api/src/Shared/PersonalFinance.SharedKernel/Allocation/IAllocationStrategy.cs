namespace PersonalFinance.SharedKernel.Allocation;

/// <summary>
/// Splits an integral <c>total</c> across parts sized by <c>weights</c>
/// </summary>
public interface IAllocationStrategy {
    IReadOnlyList<long> Allocate(long total, IReadOnlyList<long> weights);
}

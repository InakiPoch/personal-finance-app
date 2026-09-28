namespace PersonalFinance.Financing.Application.Commands;

/// <summary>
/// Splits an amount across installments in the given order: each takes min(remaining, left). Stops as
/// soon as the amount is used up, so installments that get nothing are not in the output.
/// </summary>
internal static class CreditorPaymentWaterfall {
    public static IReadOnlyList<(Guid InstallmentId, long AmountMinorUnits)> Allocate(
        IEnumerable<(Guid InstallmentId, long RemainingMinorUnits)> ordered, long amountMinorUnits) {
        var result = new List<(Guid InstallmentId, long AmountMinorUnits)>();
        var left = amountMinorUnits;
        foreach(var (installmentId, remaining) in ordered) {
            if(left == 0) {
                break;
            }
            var take = Math.Min(left, remaining);
            result.Add((installmentId, take));
            left -= take;
        }
        return result;
    }
}

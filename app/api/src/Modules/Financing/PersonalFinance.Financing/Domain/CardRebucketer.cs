using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Domain;

/// <summary>
/// Re-buckets a card's payment plans after a closing-day change: a plan whose purchase now resolves to a different
/// billing cycle has all its installments shifted by the same month delta. Refuses (and moves nothing) if any plan that
/// would move already has a charged installment.
/// </summary>
internal static class CardRebucketer {
    public static Result Rebucket(CreditCard card, IEnumerable<PaymentPlan> plans) {
        var moves = new List<(PaymentPlan Plan, int Delta)>();
        foreach(var plan in plans) {
            var live = plan.Installments.Where(installment => !installment.IsReversed).OrderBy(installment => installment.Sequence).ToList();
            if(live.Count == 0) {
                continue;
            }
            var first = plan.Installments.OrderBy(installment => installment.Sequence).First();
            var target = card.ResolveCycle(plan.PurchaseDate);
            var delta = ordinalOf(target) - ordinalOf(first.Cycle);
            if(delta == 0) {
                continue;
            }
            if(plan.Installments.Any(installment => installment.IsAccrued)) {
                return Result.Failure(FinancingErrors.ClosingChangeMovesChargedPurchase);
            }
            moves.Add((plan, delta));
        }
        foreach(var (plan, delta) in moves) {
            foreach(var installment in plan.Installments) {
                var moved = installment.MoveToCycle(installment.Cycle.AddMonths(delta));
                if(moved.IsFailure) {
                    return moved;
                }
            }
        }
        return Result.Success();
    }

    private static int ordinalOf(BillingCycle cycle) {
        return (cycle.Year * 12) + cycle.Month;
    }
}

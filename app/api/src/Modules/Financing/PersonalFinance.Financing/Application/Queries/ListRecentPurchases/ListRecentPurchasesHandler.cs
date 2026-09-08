using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;

namespace PersonalFinance.Financing.Application.Queries.ListRecentPurchases;

internal sealed class ListRecentPurchasesHandler(FinancingDbContext context) : IQueryHandler<ListRecentPurchasesQuery, RecentPurchasesResponse> {
    public async Task<RecentPurchasesResponse> HandleAsync(ListRecentPurchasesQuery query, CancellationToken cancellationToken) {
        var cardNames = await context.CreditCards
            .Select(card => new { card.Id, card.Name })
            .ToDictionaryAsync(card => card.Id, card => card.Name, cancellationToken);
        var plans = await context.PaymentPlans
            .Select(plan => new {
                plan.Id,
                plan.Description,
                plan.CardId,
                plan.PurchaseDate,
                plan.Total,
                plan.InstallmentCount,
                plan.CreditorId
            })
            .ToListAsync(cancellationToken);
        var recentPlans = plans
            .OrderByDescending(plan => plan.PurchaseDate)
            .Take(query.Limit)
            .ToList();
        var planIds = recentPlans.Select(plan => plan.Id).ToList();
        var installments = await context.Set<Installment>()
            .Where(installment => planIds.Contains(installment.PaymentPlanId))
            .Select(installment => new {
                installment.PaymentPlanId,
                installment.Sequence,
                installment.IsReversed,
                installment.PaidOnUtc,
                installment.CycleYear,
                installment.CycleMonth,
                installment.Amount
            })
            .ToListAsync(cancellationToken);
        var installmentsByPlan = installments.ToLookup(installment => installment.PaymentPlanId);
        var rows = recentPlans
            .Select(plan => {
                var planInstallments = installmentsByPlan[plan.Id].ToList();
                var paidInstallmentCount = planInstallments.Count(installment => installment.PaidOnUtc is not null);
                var unpaidInstallments = planInstallments
                    .Where(installment => installment.PaidOnUtc is null && installment.IsReversed == false)
                    .ToList();
                var pendingAmountMinorUnits = unpaidInstallments.Sum(installment => installment.Amount.MinorUnits);
                var nextDue = unpaidInstallments
                    .OrderBy(installment => installment.Sequence)
                    .Select(installment => new BillingCycle(installment.CycleYear, installment.CycleMonth).DueCycle)
                    .FirstOrDefault();
                return new RecentPurchaseRow(
                    plan.Id,
                    plan.Description,
                    plan.CardId is { } cardId ? cardNames.GetValueOrDefault(cardId, "") : "",
                    plan.PurchaseDate,
                    plan.Total.MinorUnits,
                    plan.InstallmentCount,
                    plan.CreditorId is not null,
                    paidInstallmentCount,
                    nextDue?.Year,
                    nextDue?.Month,
                    pendingAmountMinorUnits
                );
            })
            .ToList();
        return new RecentPurchasesResponse(rows);
    }
}

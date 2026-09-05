using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts.Queries;
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
        var rows = plans
            .OrderByDescending(plan => plan.PurchaseDate)
            .Take(query.Limit)
            .Select(plan => new RecentPurchaseRow(
                plan.Id,
                plan.Description,
                cardNames.GetValueOrDefault(plan.CardId, ""),
                plan.PurchaseDate,
                plan.Total.MinorUnits,
                plan.InstallmentCount,
                plan.CreditorId is not null))
            .ToList();
        return new RecentPurchasesResponse(rows);
    }
}

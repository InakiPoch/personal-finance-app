using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;

namespace PersonalFinance.Financing.Application.Queries.GetCardPurchases;

internal sealed class GetCardPurchasesHandler(FinancingDbContext context, TimeProvider timeProvider) : IQueryHandler<GetCardPurchasesQuery, CardPurchasesResponse> {
    public async Task<CardPurchasesResponse> HandleAsync(GetCardPurchasesQuery query, CancellationToken cancellationToken) {
        var card = await context.CreditCards
            .Include(candidate => candidate.ClosingOverrides)
            .FirstOrDefaultAsync(candidate => candidate.Id == query.CardId, cancellationToken);
        var statements = await context.MonthlyStatements
            .Where(statement => statement.CardId == query.CardId)
            .Select(statement => new { statement.Id, statement.PaidOnUtc })
            .ToListAsync(cancellationToken);
        var isPaidByStatementId = statements.ToDictionary(statement => statement.Id, statement => statement.PaidOnUtc is not null);
        var installments = await (
            from installment in context.Set<Installment>()
            where installment.IsReversed == false
            join plan in context.PaymentPlans on installment.PaymentPlanId equals plan.Id
            where plan.CardId == query.CardId
            select new {
                PlanId = plan.Id,
                plan.Description,
                plan.Total,
                plan.InstallmentCount,
                plan.PurchaseDate,
                installment.AccruedOnUtc,
                installment.StatementId,
                installment.CycleYear,
                installment.CycleMonth
            }
        ).ToListAsync(cancellationToken);
        var today = query.Today ?? DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var selected = query.Month is { } month ? new BillingCycle(month.Year, month.Month) : null;
        var isCurrentMonth = selected is not null && selected == new BillingCycle(today.Year, today.Month);
        var outstanding = installments
            .Where(row => {
                var isAccruedUnpaid = row.StatementId is not null
                    && isPaidByStatementId.TryGetValue(row.StatementId.Value, out var isPaid)
                    && !isPaid;
                if(selected is null) {
                    return row.AccruedOnUtc is null || isAccruedUnpaid;
                }
                if(row.AccruedOnUtc is not null) {
                    return isCurrentMonth && isAccruedUnpaid;
                }
                var due = new BillingCycle(row.CycleYear, row.CycleMonth).DueCycle;
                return due == selected || (isCurrentMonth && due == selected.AddMonths(1));
            })
            .ToList();
        var currentCycle = card?.ResolveCycle(today);
        var rows = outstanding
            .GroupBy(row => row.PlanId)
            .Select(group => new {
                First = group.First(),
                OutstandingCount = group.Count(),
                IsCurrentCycle = currentCycle is not null && group.Any(row => row.CycleYear == currentCycle.Year && row.CycleMonth == currentCycle.Month)
            })
            .OrderByDescending(entry => entry.IsCurrentCycle)
            .ThenByDescending(entry => entry.First.PurchaseDate)
            .Select(entry => new CardPurchaseRow(
                entry.First.PlanId,
                entry.First.Description,
                entry.First.Total.MinorUnits,
                entry.First.InstallmentCount,
                entry.OutstandingCount,
                entry.First.PurchaseDate))
            .ToList();
        return new CardPurchasesResponse(query.CardId, rows);
    }
}

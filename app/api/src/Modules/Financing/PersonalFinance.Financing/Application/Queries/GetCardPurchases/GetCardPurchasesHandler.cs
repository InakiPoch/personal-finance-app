using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;

namespace PersonalFinance.Financing.Application.Queries.GetCardPurchases;

internal sealed class GetCardPurchasesHandler(FinancingDbContext context) : IQueryHandler<GetCardPurchasesQuery, CardPurchasesResponse> {
    public async Task<CardPurchasesResponse> HandleAsync(GetCardPurchasesQuery query, CancellationToken cancellationToken) {
        var cutoffDay = await context.CreditCards
            .Where(card => card.Id == query.CardId)
            .Select(card => card.CutoffDay)
            .FirstOrDefaultAsync(cancellationToken);
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
        var outstanding = installments
            .Where(row => row.AccruedOnUtc is null
                || (row.StatementId is not null
                    && isPaidByStatementId.TryGetValue(row.StatementId.Value, out var isPaid)
                    && !isPaid))
            .ToList();
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        var currentCycle = BillingCycleCalculator.ResolveCycle(today, cutoffDay);
        var rows = outstanding
            .GroupBy(row => row.PlanId)
            .Select(group => new {
                First = group.First(),
                OutstandingCount = group.Count(),
                IsCurrentCycle = group.Any(row => row.CycleYear == currentCycle.Year && row.CycleMonth == currentCycle.Month)
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

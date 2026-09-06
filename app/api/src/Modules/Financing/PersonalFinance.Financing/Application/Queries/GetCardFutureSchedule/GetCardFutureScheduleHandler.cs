using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;

namespace PersonalFinance.Financing.Application.Queries.GetCardFutureSchedule;

internal sealed class GetCardFutureScheduleHandler(FinancingDbContext context) : IQueryHandler<GetCardFutureScheduleQuery, CardFutureScheduleResponse> {
    public async Task<CardFutureScheduleResponse> HandleAsync(GetCardFutureScheduleQuery query, CancellationToken cancellationToken) {
        var pending = await (
            from installment in context.Set<Installment>()
            where installment.AccruedOnUtc == null
            where installment.IsReversed == false
            join plan in context.PaymentPlans on installment.PaymentPlanId equals plan.Id
            where plan.CardId == query.CardId
            orderby installment.CycleYear, installment.CycleMonth, installment.Sequence
            select new {
                PlanId = plan.Id,
                installment.Id,
                installment.Sequence,
                installment.CycleYear,
                installment.CycleMonth,
                installment.Amount
            }
        ).ToListAsync(cancellationToken);
        var rows = pending
            .Select(row => {
                var dueCycle = new BillingCycle(row.CycleYear, row.CycleMonth).DueCycle;
                return new CardFutureScheduleRow(
                    row.PlanId,
                    row.Id,
                    row.Sequence,
                    dueCycle.Year,
                    dueCycle.Month,
                    row.Amount.MinorUnits);
            })
            .ToList();
        return new CardFutureScheduleResponse(rows);
    }
}

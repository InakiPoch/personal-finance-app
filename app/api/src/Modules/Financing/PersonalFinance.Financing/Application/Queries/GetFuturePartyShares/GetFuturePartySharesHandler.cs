using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.SharedKernel.Allocation;

namespace PersonalFinance.Financing.Application.Queries.GetFuturePartyShares;

/// <summary>
/// Projects the not-yet-accrued installment shares a party will owe on its card-split plans, one row per upcoming billing cycle.
/// </summary>
internal sealed class GetFuturePartySharesHandler(FinancingDbContext context) : IQueryHandler<GetFuturePartySharesQuery, GetFuturePartySharesResponse> {
    public async Task<GetFuturePartySharesResponse> HandleAsync(GetFuturePartySharesQuery query, CancellationToken cancellationToken) {
        var pending = await (
            from installment in context.Set<Installment>()
            where installment.AccruedOnUtc == null
            where installment.IsReversed == false
            join plan in context.PaymentPlans on installment.PaymentPlanId equals plan.Id
            where plan.CardId != null
            where plan.SplitParticipants.Any(participant => participant.PartyId == query.PartyId)
            from card in context.CreditCards.Where(candidate => candidate.Id == plan.CardId)
            select new { installment, plan, CardName = card.Name }
        ).ToListAsync(cancellationToken);
        if(pending.Count == 0) {
            return new GetFuturePartySharesResponse([]);
        }
        var planIds = pending.Select(row => row.plan.Id).Distinct().ToList();
        var participantsByPlan = (await context.Set<PaymentPlanSplitParticipant>()
                .Where(participant => planIds.Contains(participant.PaymentPlanId))
                .ToListAsync(cancellationToken))
            .GroupBy(participant => participant.PaymentPlanId)
            .ToDictionary(group => group.Key, group => group.OrderBy(participant => participant.PartyId).ToList());
        var allocator = new PhantomPennyAllocator();
        var rows = new List<FuturePartyShareRow>();
        foreach(var row in pending) {
            var participants = participantsByPlan[row.plan.Id];
            var index = participants.FindIndex(participant => participant.PartyId == query.PartyId);
            if(index < 0) {
                continue;
            }
            long[] weights = [1L, .. participants.Select(participant => participant.Weight)];
            var shares = allocator.Allocate(row.installment.Amount, weights);
            var share = shares[index + 1];
            if(share.MinorUnits <= 0) {
                continue;
            }
            rows.Add(new FuturePartyShareRow(
                row.installment.CycleYear,
                row.installment.CycleMonth,
                share.MinorUnits,
                share.Currency.Code,
                $"{row.CardName} — {row.plan.Description}"
            ));
        }
        var ordered = rows
            .OrderBy(shareRow => shareRow.CycleYear)
            .ThenBy(shareRow => shareRow.CycleMonth)
            .ToList();
        return new GetFuturePartySharesResponse(ordered);
    }
}

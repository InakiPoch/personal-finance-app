using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.SharedKernel.Allocation;

namespace PersonalFinance.Financing.Application.Queries.GetPendingSharesByParty;

internal sealed class GetPendingSharesByPartyHandler(FinancingDbContext context) : IQueryHandler<GetPendingSharesByPartyQuery, GetPendingSharesByPartyResponse> {
    public async Task<GetPendingSharesByPartyResponse> HandleAsync(GetPendingSharesByPartyQuery query, CancellationToken cancellationToken) {
        var pending = await (
            from installment in context.Set<Installment>()
            where installment.SplitAccruedOnUtc == null
            where installment.IsReversed == false
            join plan in context.PaymentPlans on installment.PaymentPlanId equals plan.Id
            where plan.SplitParticipants.Any()
            select new { installment, PlanId = plan.Id }
        ).ToListAsync(cancellationToken);
        if(pending.Count == 0) {
            return new GetPendingSharesByPartyResponse([]);
        }
        var planIds = pending.Select(row => row.PlanId).Distinct().ToList();
        var participantsByPlan = (await context.Set<PaymentPlanSplitParticipant>()
                .Where(participant => planIds.Contains(participant.PaymentPlanId))
                .ToListAsync(cancellationToken))
            .GroupBy(participant => participant.PaymentPlanId)
            .ToDictionary(group => group.Key, group => group.OrderBy(participant => participant.PartyId).ToList());
        var allocator = new PhantomPennyAllocator();
        var accumulatorByParty = new Dictionary<Guid, PartyPendingAccumulator>();
        foreach(var row in pending) {
            if(!participantsByPlan.TryGetValue(row.PlanId, out var participants)) {
                continue;
            }
            long[] weights = [1L, .. participants.Select(participant => participant.Weight)];
            var shares = allocator.Allocate(row.installment.Amount, weights);
            for(var index = 0; index < participants.Count; index++) {
                var share = shares[index + 1];
                if(share.MinorUnits <= 0) {
                    continue;
                }
                var partyId = participants[index].PartyId;
                if(!accumulatorByParty.TryGetValue(partyId, out var accumulator)) {
                    accumulator = new PartyPendingAccumulator(share.Currency.Code);
                    accumulatorByParty[partyId] = accumulator;
                }
                accumulator.Add(share.MinorUnits);
            }
        }
        var rows = accumulatorByParty
            .Select(pair => new PendingSharesByPartyRow(pair.Key, pair.Value.Count, pair.Value.TotalMinorUnits, pair.Value.CurrencyCode))
            .OrderBy(row => row.PartyId)
            .ToList();
        return new GetPendingSharesByPartyResponse(rows);
    }

    private sealed class PartyPendingAccumulator(string currencyCode) {
        public string CurrencyCode { get; } = currencyCode;
        public int Count { get; private set; }
        public long TotalMinorUnits { get; private set; }

        public void Add(long minorUnits) {
            Count++;
            TotalMinorUnits += minorUnits;
        }
    }
}

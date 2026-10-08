using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;
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
        if(query.ThroughMonth is { } through) {
            var throughOrdinal = through.MonthOrdinal();
            pending = pending.Where(row => new DateOnly(row.installment.DueCycle.Year, row.installment.DueCycle.Month, 1).MonthOrdinal() <= throughOrdinal).ToList();
        }
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
        var accumulatorByPartyCurrency = new Dictionary<(Guid PartyId, string Currency), PendingShareAccumulator>();
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
                var key = (participants[index].PartyId, share.Currency.Code);
                if(!accumulatorByPartyCurrency.TryGetValue(key, out var accumulator)) {
                    accumulator = new PendingShareAccumulator(share.Currency.Code);
                    accumulatorByPartyCurrency[key] = accumulator;
                }
                accumulator.Add(share.MinorUnits);
            }
        }
        var rows = accumulatorByPartyCurrency
            .Select(pair => new PendingSharesByPartyRow(pair.Key.PartyId, pair.Value.Count, pair.Value.TotalMinorUnits, pair.Value.CurrencyCode))
            .OrderBy(row => row.PartyId)
            .ThenBy(row => row.CurrencyCode, StringComparer.Ordinal)
            .ToList();
        return new GetPendingSharesByPartyResponse(rows);
    }

    private sealed class PendingShareAccumulator(string currencyCode) {
        public string CurrencyCode { get; } = currencyCode;
        public int Count { get; private set; }
        public long TotalMinorUnits { get; private set; }

        public void Add(long minorUnits) {
            Count++;
            TotalMinorUnits += minorUnits;
        }
    }
}

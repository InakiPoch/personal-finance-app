using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Application.Commands.LinkPaymentPlanSplit;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.Parties.Contracts;
using PersonalFinance.Parties.Contracts.Queries;

namespace PersonalFinance.Financing.Application.Queries.GetCreditorDetail;

/// <summary>
/// Read-only drill-down for one creditor: every creditor-financed purchase (payment plan) with its installments beneath.
/// </summary>
internal sealed class GetCreditorDetailHandler(FinancingDbContext context, TimeProvider timeProvider, IPartiesApi partiesApi) : IQueryHandler<GetCreditorDetailQuery, CreditorDetailResponse> {
    public async Task<CreditorDetailResponse> HandleAsync(GetCreditorDetailQuery query, CancellationToken cancellationToken) {
        var creditor = await context.Creditors
            .FirstOrDefaultAsync(candidate => candidate.Id == query.CreditorId, cancellationToken);
        if(creditor is null) {
            return new CreditorDetailResponse(false, query.CreditorId, "", []);
        }
        var rows = await (
            from installment in context.Set<Installment>()
            join plan in context.PaymentPlans on installment.PaymentPlanId equals plan.Id
            where plan.CreditorId == query.CreditorId
            select new {
                plan.Id,
                plan.Description,
                plan.PurchaseDate,
                InstallmentId = installment.Id,
                installment.Sequence,
                installment.Amount,
                installment.CycleYear,
                installment.CycleMonth,
                installment.PaidOnUtc,
                installment.IsReversed,
                installment.SplitAccruedOnUtc,
                PaidMinorUnits = context.Set<CreditorInstallmentPayment>()
                    .Where(payment => payment.InstallmentId == installment.Id)
                .Sum(payment => (long?)payment.AmountMinorUnits) ?? 0
            }
        ).ToListAsync(cancellationToken);
        var planIds = rows.Select(row => row.Id).Distinct().ToList();
        var participantsByPlan = (await context.Set<PaymentPlanSplitParticipant>()
                .Where(participant => planIds.Contains(participant.PaymentPlanId))
                .OrderBy(participant => participant.PartyId)
                .ToListAsync(cancellationToken))
            .GroupBy(participant => participant.PaymentPlanId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<PaymentPlanSplitParticipant>)group.ToList());
        var installmentIds = rows.Select(row => row.InstallmentId).ToList();
        var paidPartyIdsByInstallment = (await context.Set<CreditorInstallmentPayment>()
                .Where(payment => installmentIds.Contains(payment.InstallmentId) && payment.PartyId != null)
                .Select(payment => new { payment.InstallmentId, PartyId = payment.PartyId!.Value })
                .ToListAsync(cancellationToken))
            .GroupBy(payment => payment.InstallmentId)
            .ToDictionary(group => group.Key, group => group.Select(payment => payment.PartyId).ToHashSet());
        var partyNamesById = (await partiesApi.ListPartiesAsync(new ListPartiesQuery(), cancellationToken)).Rows
            .ToDictionary(party => party.Id, party => party.Name);
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var currentDueCycle = BillingCycleCalculator.ResolveCycle(today, PaymentPlan.CreditorCutoffDay).DueCycle;
        var currentOrdinal = currentDueCycle.Year * 12 + currentDueCycle.Month;
        var purchases = rows
            .GroupBy(row => row.Id)
            .Select(group => {
                var first = group.First();
                var installmentCount = group.Count();
                var installments = group
                    .OrderBy(row => row.Sequence)
                    .Select(row => {
                        var dueCycle = new BillingCycle(row.CycleYear, row.CycleMonth).DueCycle;
                        var dueOrdinal = dueCycle.Year * 12 + dueCycle.Month;
                        var partyShares = row.IsReversed || row.SplitAccruedOnUtc is null
                            || !participantsByPlan.TryGetValue(row.Id, out var participants)
                            ? []
                            : CreditorSplitReceivableCalculator.PartyShares(row.Amount, participants)
                                .Where(share => share.ShareMinorUnits > 0)
                                .Select(share => new CreditorInstallmentPartyShare(
                                    share.PartyId,
                                    partyNamesById.GetValueOrDefault(share.PartyId, "Unknown party"),
                                    share.ShareMinorUnits,
                                    paidPartyIdsByInstallment.TryGetValue(row.InstallmentId, out var paidSet) && paidSet.Contains(share.PartyId))
                                )
                                .ToList();
                        return new CreditorInstallmentRow(
                            row.InstallmentId,
                            row.Sequence,
                            installmentCount,
                            row.Amount.MinorUnits,
                            dueCycle.Year,
                            dueCycle.Month,
                            row.PaidOnUtc is not null,
                            row.IsReversed,
                            statusFor(row.IsReversed, row.PaidOnUtc, dueOrdinal, currentOrdinal),
                            row.PaidMinorUnits,
                            row.Amount.MinorUnits - row.PaidMinorUnits,
                            row.PaidMinorUnits > 0,
                            partyShares
                        );
                    })
                    .ToList();
                var nonReversed = group.Where(row => row.IsReversed == false).ToList();
                var totalMinorUnits = nonReversed.Sum(row => row.Amount.MinorUnits);
                var outstandingMinorUnits = nonReversed.Sum(row => row.Amount.MinorUnits - row.PaidMinorUnits);
                return new CreditorPurchaseGroup(
                    group.Key,
                    first.Description,
                    first.PurchaseDate,
                    totalMinorUnits,
                    outstandingMinorUnits,
                    installments,
                    first.Amount.Currency.Code
                );
            })
            .OrderByDescending(purchase => purchase.PurchaseDate)
            .ToList();
        return new CreditorDetailResponse(true, creditor.Id, creditor.Name, purchases);
    }

    private static string statusFor(bool isReversed, DateTimeOffset? paidOnUtc, int dueOrdinal, int currentOrdinal) {
        if(isReversed) {
            return "reversed";
        }
        if(paidOnUtc is not null) {
            return "paid";
        }
        if(dueOrdinal < currentOrdinal) {
            return "overdue";
        }
        return dueOrdinal == currentOrdinal ? "due" : "future";
    }
}

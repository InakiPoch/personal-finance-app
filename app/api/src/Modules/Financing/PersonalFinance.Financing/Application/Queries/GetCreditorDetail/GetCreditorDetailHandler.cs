using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;

namespace PersonalFinance.Financing.Application.Queries.GetCreditorDetail;

/// <summary>
/// Read-only drill-down for one creditor: every creditor-financed purchase (payment plan) with its
/// installments beneath. Per group, <c>Total</c> is the sum of non-reversed installment amounts and
/// <c>Outstanding</c> the sum of the unpaid, non-reversed ones. Each installment's status compares its
/// payment cycle (<see cref="Installment.DueCycle"/>) to the current uniform-26th creditor cycle.
/// </summary>
internal sealed class GetCreditorDetailHandler(FinancingDbContext context, TimeProvider timeProvider) : IQueryHandler<GetCreditorDetailQuery, CreditorDetailResponse> {
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
                installment.IsReversed
            }
        ).ToListAsync(cancellationToken);
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
                        return new CreditorInstallmentRow(
                            row.InstallmentId,
                            row.Sequence,
                            installmentCount,
                            row.Amount.MinorUnits,
                            dueCycle.Year,
                            dueCycle.Month,
                            row.PaidOnUtc is not null,
                            row.IsReversed,
                            statusFor(row.IsReversed, row.PaidOnUtc, dueOrdinal, currentOrdinal)
                        );
                    })
                    .ToList();
                var nonReversed = group.Where(row => row.IsReversed == false).ToList();
                var totalMinorUnits = nonReversed.Sum(row => row.Amount.MinorUnits);
                var outstandingMinorUnits = nonReversed
                    .Where(row => row.PaidOnUtc is null)
                    .Sum(row => row.Amount.MinorUnits);
                return new CreditorPurchaseGroup(
                    group.Key,
                    first.Description,
                    first.PurchaseDate,
                    totalMinorUnits,
                    outstandingMinorUnits,
                    installments
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

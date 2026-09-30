using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Application.Queries.Shared;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;

namespace PersonalFinance.Financing.Application.Queries.GetDueThisMonth;

/// <summary>
/// Read-only dashboard "Due this month": unpaid card installments due by the current calendar month
/// (overdue included) plus creditor remaining amounts due by the current calendar month (overdue included).
/// </summary>
internal sealed class GetDueThisMonthHandler(FinancingDbContext context, TimeProvider timeProvider) : IQueryHandler<GetDueThisMonthQuery, DueThisMonthResponse> {
    public async Task<DueThisMonthResponse> HandleAsync(GetDueThisMonthQuery query, CancellationToken cancellationToken) {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var rows = new List<DueThisMonthRow>();
        rows.AddRange(await buildCardRowsAsync(today, cancellationToken));
        rows.AddRange(await buildCreditorRowsAsync(today, cancellationToken));
        var ordered = rows
            .OrderBy(row => row.Kind, StringComparer.Ordinal)
            .ThenBy(row => row.SourceName, StringComparer.Ordinal)
            .ThenBy(row => row.CurrencyCode, StringComparer.Ordinal)
            .ToList();
        return new DueThisMonthResponse(ordered);
    }

    private async Task<List<DueThisMonthRow>> buildCardRowsAsync(DateOnly today, CancellationToken cancellationToken) {
        var installments = await (
            from installment in context.Set<Installment>()
            where installment.IsReversed == false && installment.PaidOnUtc == null
            join plan in context.PaymentPlans on installment.PaymentPlanId equals plan.Id
            where plan.CardId != null
            select new { plan.CardId, installment.Amount, installment.CycleYear, installment.CycleMonth }
        ).ToListAsync(cancellationToken);
        var currentOrdinal = today.Year * 12 + today.Month;
        var cardNameById = await context.CreditCards.ToDictionaryAsync(card => card.Id, card => card.Name, cancellationToken);
        return installments
            .Where(row => {
                var dueCycle = new BillingCycle(row.CycleYear, row.CycleMonth).DueCycle;
                return dueCycle.Year * 12 + dueCycle.Month <= currentOrdinal;
            })
            .GroupBy(row => new { CardId = row.CardId!.Value, row.Amount.Currency.Code })
            .Select(group => new DueThisMonthRow(
                "card",
                group.Key.CardId,
                cardNameById.TryGetValue(group.Key.CardId, out var name) ? name : "",
                group.Key.Code,
                group.Sum(row => row.Amount.MinorUnits)))
            .Where(row => row.AmountMinorUnits > 0)
        .ToList();
    }

    private async Task<List<DueThisMonthRow>> buildCreditorRowsAsync(DateOnly today, CancellationToken cancellationToken) {
        var installments = await (
            from installment in context.Set<Installment>()
            where installment.IsReversed == false
            join plan in context.PaymentPlans on installment.PaymentPlanId equals plan.Id
            where plan.CreditorId != null
            select new {
                plan.CreditorId,
                installment.Amount,
                installment.CycleYear,
                installment.CycleMonth,
                PaidMinorUnits = context.Set<CreditorInstallmentPayment>()
                    .Where(payment => payment.InstallmentId == installment.Id)
                    .Sum(payment => (long?)payment.AmountMinorUnits) ?? 0
            }
        ).ToListAsync(cancellationToken);
        var currentOrdinal = today.Year * 12 + today.Month;
        var creditorNameById = await context.Creditors.ToDictionaryAsync(creditor => creditor.Id, creditor => creditor.Name, cancellationToken);
        return installments
            .Where(row => CreditorDueNowHelper.IsDueNow(row.CycleYear, row.CycleMonth, currentOrdinal))
            .GroupBy(row => new { CreditorId = row.CreditorId!.Value, row.Amount.Currency.Code })
            .Select(group => new DueThisMonthRow(
                "creditor",
                group.Key.CreditorId,
                creditorNameById.TryGetValue(group.Key.CreditorId, out var name) ? name : "",
                group.Key.Code,
                group.Sum(row => CreditorDueNowHelper.RemainingMinorUnits(row.Amount.MinorUnits, row.PaidMinorUnits))))
            .Where(row => row.AmountMinorUnits > 0)
        .ToList();
    }
}

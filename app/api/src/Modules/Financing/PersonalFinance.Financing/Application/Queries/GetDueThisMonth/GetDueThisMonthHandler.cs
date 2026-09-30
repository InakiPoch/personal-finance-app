using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Application.Queries.Shared;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;

namespace PersonalFinance.Financing.Application.Queries.GetDueThisMonth;

/// <summary>
/// Read-only dashboard "Due this month": card installments due by the requested calendar month (overdue included)
/// plus creditor remaining amounts due by that month. For the current month (or no month) paid amounts are excluded
/// as of now; for any other month, only amounts paid before the first instant of that month are excluded.
/// </summary>
internal sealed class GetDueThisMonthHandler(FinancingDbContext context, TimeProvider timeProvider) : IQueryHandler<GetDueThisMonthQuery, DueThisMonthResponse> {
    public async Task<DueThisMonthResponse> HandleAsync(GetDueThisMonthQuery query, CancellationToken cancellationToken) {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var requested = query.Month ?? today;
        var requestedOrdinal = requested.Year * 12 + requested.Month;
        var isCurrentMonth = requestedOrdinal == today.Year * 12 + today.Month;
        var paidBefore = isCurrentMonth ? DateTimeOffset.MaxValue : new DateTimeOffset(requested.Year, requested.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var rows = new List<DueThisMonthRow>();
        rows.AddRange(await buildCardRowsAsync(requestedOrdinal, paidBefore, cancellationToken));
        rows.AddRange(await buildCreditorRowsAsync(requestedOrdinal, paidBefore, cancellationToken));
        var ordered = rows
            .OrderBy(row => row.Kind, StringComparer.Ordinal)
            .ThenBy(row => row.SourceName, StringComparer.Ordinal)
            .ThenBy(row => row.CurrencyCode, StringComparer.Ordinal)
            .ToList();
        return new DueThisMonthResponse(ordered);
    }

    private async Task<List<DueThisMonthRow>> buildCardRowsAsync(int requestedOrdinal, DateTimeOffset paidBefore, CancellationToken cancellationToken) {
        var installments = await (
            from installment in context.Set<Installment>()
            where installment.IsReversed == false
            join plan in context.PaymentPlans on installment.PaymentPlanId equals plan.Id
            where plan.CardId != null
            select new { plan.CardId, installment.Amount, installment.CycleYear, installment.CycleMonth, installment.PaidOnUtc }
        ).ToListAsync(cancellationToken);
        var cardNameById = await context.CreditCards.ToDictionaryAsync(card => card.Id, card => card.Name, cancellationToken);
        return installments
            .Where(row => {
                var dueCycle = new BillingCycle(row.CycleYear, row.CycleMonth).DueCycle;
                var settledBefore = row.PaidOnUtc is { } paidOn && paidOn < paidBefore;
                return dueCycle.Year * 12 + dueCycle.Month <= requestedOrdinal && !settledBefore;
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

    private async Task<List<DueThisMonthRow>> buildCreditorRowsAsync(int requestedOrdinal, DateTimeOffset paidBefore, CancellationToken cancellationToken) {
        var installments = await (
            from installment in context.Set<Installment>()
            where installment.IsReversed == false
            join plan in context.PaymentPlans on installment.PaymentPlanId equals plan.Id
            where plan.CreditorId != null
            select new { installment.Id, plan.CreditorId, installment.Amount, installment.CycleYear, installment.CycleMonth }
        ).ToListAsync(cancellationToken);
        // Filtered in memory: SQLite cannot compare DateTimeOffset columns server-side.
        var payments = await context.Set<CreditorInstallmentPayment>()
            .Select(payment => new { payment.InstallmentId, payment.AmountMinorUnits, payment.PaidOnUtc })
            .ToListAsync(cancellationToken);
        var paidByInstallment = payments
            .Where(payment => payment.PaidOnUtc < paidBefore)
            .GroupBy(payment => payment.InstallmentId)
            .ToDictionary(group => group.Key, group => group.Sum(payment => payment.AmountMinorUnits));
        var creditorNameById = await context.Creditors.ToDictionaryAsync(creditor => creditor.Id, creditor => creditor.Name, cancellationToken);
        return installments
            .Where(row => CreditorDueNowHelper.IsDueNow(row.CycleYear, row.CycleMonth, requestedOrdinal))
            .GroupBy(row => new { CreditorId = row.CreditorId!.Value, row.Amount.Currency.Code })
            .Select(group => new DueThisMonthRow(
                "creditor",
                group.Key.CreditorId,
                creditorNameById.TryGetValue(group.Key.CreditorId, out var name) ? name : "",
                group.Key.Code,
                group.Sum(row => CreditorDueNowHelper.RemainingMinorUnits(row.Amount.MinorUnits, paidByInstallment.GetValueOrDefault(row.Id)))))
            .Where(row => row.AmountMinorUnits > 0)
        .ToList();
    }
}

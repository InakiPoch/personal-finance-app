using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;

namespace PersonalFinance.Financing.Application.Queries.GetCreditorPayables;

/// <summary>
/// Read-only "Owed to creditors" list: every creditor-financed installment that has not been reversed,
/// grouped by creditor, with the amount due by the current billing cycle (arrears folded in), the whole
/// remaining debt, the earliest owed date, and the per-account breakdown. Paid installments are excluded
/// from both money figures.
/// </summary>
internal sealed class GetCreditorPayablesHandler(FinancingDbContext context, TimeProvider timeProvider) : IQueryHandler<GetCreditorPayablesQuery, CreditorPayablesResponse> {
    public async Task<CreditorPayablesResponse> HandleAsync(GetCreditorPayablesQuery query, CancellationToken cancellationToken) {
        var installments = await (
            from installment in context.Set<Installment>()
            where installment.IsReversed == false
            join plan in context.PaymentPlans on installment.PaymentPlanId equals plan.Id
            where plan.CreditorId != null
            select new {
                plan.CreditorId,
                plan.CreditorAccountId,
                installment.Amount,
                installment.CycleYear,
                installment.CycleMonth,
                installment.PaidOnUtc,
                plan.PurchaseDate
            }
        ).ToListAsync(cancellationToken);
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var currentDueCycle = BillingCycleCalculator.ResolveCycle(today, PaymentPlan.CreditorCutoffDay).DueCycle;
        var currentOrdinal = currentDueCycle.Year * 12 + currentDueCycle.Month;
        var creditors = await context.Creditors
            .Include(creditor => creditor.Accounts)
            .ToListAsync(cancellationToken);
        var creditorNameById = creditors.ToDictionary(creditor => creditor.Id, creditor => creditor.Name);
        var accountLabelById = creditors
            .SelectMany(creditor => creditor.Accounts)
            .ToDictionary(account => account.Id, account => account.Label);
        var rows = installments
            .GroupBy(row => row.CreditorId!.Value)
            .Select(group => {
                var unpaid = group.Where(row => row.PaidOnUtc is null).ToList();
                var totalOwedMinorUnits = unpaid.Sum(row => row.Amount.MinorUnits);
                var dueNowMinorUnits = unpaid
                    .Where(row => {
                        var dueCycle = new BillingCycle(row.CycleYear, row.CycleMonth).DueCycle;
                        return dueCycle.Year * 12 + dueCycle.Month <= currentOrdinal;
                    })
                    .Sum(row => row.Amount.MinorUnits);
                var earliest = group
                    .OrderBy(row => row.CycleYear)
                    .ThenBy(row => row.CycleMonth)
                    .FirstOrDefault();
                DateOnly? nextDueDate = null;
                if(earliest is not null) {
                    var dueCycle = new BillingCycle(earliest.CycleYear, earliest.CycleMonth).DueCycle;
                    nextDueDate = buildDueDate(earliest.PurchaseDate, dueCycle.Year, dueCycle.Month);
                }
                var accounts = group
                    .Where(row => row.CreditorAccountId != null)
                    .GroupBy(row => row.CreditorAccountId!.Value)
                    .Select(accountGroup => new CreditorPayableAccountBreakdown(
                        accountGroup.Key,
                        accountLabelById.TryGetValue(accountGroup.Key, out var label) ? label : "",
                        accountGroup.Sum(row => row.Amount.MinorUnits)))
                    .OrderBy(account => account.Label)
                    .ToList();
                return new CreditorPayableRow(
                    group.Key,
                    creditorNameById.TryGetValue(group.Key, out var name) ? name : "",
                    dueNowMinorUnits,
                    totalOwedMinorUnits,
                    nextDueDate,
                    accounts
                );
            })
            .OrderBy(row => row.CreditorName)
            .ToList();
        return new CreditorPayablesResponse(rows);
    }

    private static DateOnly buildDueDate(DateOnly purchaseDate, int cycleYear, int cycleMonth) {
        var day = Math.Min(purchaseDate.Day, DateTime.DaysInMonth(cycleYear, cycleMonth));
        return new DateOnly(cycleYear, cycleMonth, day);
    }
}

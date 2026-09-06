using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;

namespace PersonalFinance.Financing.Application.Queries.GetCreditorPayables;

/// <summary>
/// Read-only "Owed to creditors" list: every creditor-financed installment that has not been reversed,
/// grouped by creditor, with the outstanding total, the earliest owed date, and the per-account breakdown.
/// </summary>
internal sealed class GetCreditorPayablesHandler(FinancingDbContext context) : IQueryHandler<GetCreditorPayablesQuery, CreditorPayablesResponse> {
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
                plan.PurchaseDate
            }
        ).ToListAsync(cancellationToken);
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
                var outstandingMinorUnits = group.Sum(row => row.Amount.MinorUnits);
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
                    outstandingMinorUnits,
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

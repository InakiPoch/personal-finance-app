using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Queries;

namespace PersonalFinance.Financing.Application.Queries.GetMonthlyStatement;

internal sealed class GetMonthlyStatementHandler(FinancingDbContext context, ILedgerApi ledger) : IQueryHandler<GetMonthlyStatementQuery, MonthlyStatementDetailResponse> {
    public async Task<MonthlyStatementDetailResponse> HandleAsync(GetMonthlyStatementQuery query, CancellationToken cancellationToken) {
        var statement = await context.MonthlyStatements
            .Where(candidate => candidate.Id == query.StatementId)
            .Select(candidate => new {
                candidate.Id,
                candidate.CardId,
                candidate.CycleYear,
                candidate.CycleMonth,
                candidate.AmountDue,
                candidate.PaidOnUtc
            })
            .FirstOrDefaultAsync(cancellationToken);
        if(statement is null) {
            return new MonthlyStatementDetailResponse(false, query.StatementId, Guid.Empty, "", 0, 0, 0, false, null, []);
        }
        var cardName = await context.CreditCards
            .Where(card => card.Id == statement.CardId)
            .Select(card => card.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "";
        var accrued = await (
            from installment in context.Set<Installment>()
            where installment.StatementId == query.StatementId
            join plan in context.PaymentPlans on installment.PaymentPlanId equals plan.Id
            select new {
                PlanId = plan.Id,
                installment.Id,
                installment.Sequence,
                plan.InstallmentCount,
                plan.PurchaseDate,
                installment.CycleYear,
                installment.CycleMonth,
                installment.Amount,
                installment.IsReversed
            }
        ).ToListAsync(cancellationToken);
        var accrualTransactionIds = await ledger.FindAccrualTransactionIdsAsync(
            new FindAccrualTransactionIdsQuery(accrued.Select(row => row.Id).ToList()),
            cancellationToken);
        var installments = accrued
            .OrderBy(row => row.PurchaseDate)
            .ThenBy(row => row.Sequence)
            .Select(row => new MonthlyStatementInstallmentRow(
                row.PlanId,
                row.Id,
                row.Sequence,
                row.InstallmentCount,
                row.PurchaseDate,
                row.CycleYear,
                row.CycleMonth,
                row.Amount.MinorUnits,
                row.IsReversed,
                accrualTransactionIds.ByInstallmentReferenceId.TryGetValue(row.Id, out var transactionId) ? transactionId : null))
            .ToList();
        return new MonthlyStatementDetailResponse(
            true,
            statement.Id,
            statement.CardId,
            cardName,
            statement.CycleYear,
            statement.CycleMonth,
            statement.AmountDue.MinorUnits,
            statement.PaidOnUtc is not null,
            statement.PaidOnUtc,
            installments
        );
    }
}

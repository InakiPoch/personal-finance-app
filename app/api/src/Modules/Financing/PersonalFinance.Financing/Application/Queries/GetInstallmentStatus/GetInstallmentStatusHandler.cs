using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;

namespace PersonalFinance.Financing.Application.Queries.GetInstallmentStatus;

internal sealed class GetInstallmentStatusHandler(FinancingDbContext context) : IQueryHandler<GetInstallmentStatusQuery, InstallmentStatusResponse> {
    public async Task<InstallmentStatusResponse> HandleAsync(GetInstallmentStatusQuery query, CancellationToken cancellationToken) {
        var row = await (
            from installment in context.Set<Installment>()
            where installment.Id == query.InstallmentId
            join plan in context.PaymentPlans on installment.PaymentPlanId equals plan.Id
            join card in context.CreditCards on plan.CardId equals card.Id
            select new { Installment = installment, Card = card }
        ).FirstOrDefaultAsync(cancellationToken);
        if(row is null) {
            return new InstallmentStatusResponse(false, false, false, null, 0, false, Guid.Empty, Guid.Empty, Guid.Empty);
        }
        var paid = false;
        if(row.Installment.StatementId is Guid statementId) {
            paid = await context.MonthlyStatements.AnyAsync(statement => statement.Id == statementId && statement.PaidOnUtc != null, cancellationToken);
        }
        return new InstallmentStatusResponse(
            true,
            row.Installment.IsAccrued,
            paid,
            row.Installment.StatementId,
            row.Installment.Amount.MinorUnits,
            row.Installment.IsReversed,
            row.Card.Id,
            row.Card.CreditAccountId,
            row.Card.LiabilityAccountId
        );
    }
}

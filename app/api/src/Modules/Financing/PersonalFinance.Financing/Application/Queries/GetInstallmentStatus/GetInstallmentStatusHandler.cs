using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;

namespace PersonalFinance.Financing.Application.Queries.GetInstallmentStatus;

internal sealed class GetInstallmentStatusHandler(FinancingDbContext context) : IQueryHandler<GetInstallmentStatusQuery, InstallmentStatusResponse> {
    public async Task<InstallmentStatusResponse> HandleAsync(GetInstallmentStatusQuery query, CancellationToken cancellationToken) {
        var installment = await context.Set<Installment>()
            .FirstOrDefaultAsync(candidate => candidate.Id == query.InstallmentId, cancellationToken);
        if(installment is null) {
            return new InstallmentStatusResponse(false, false, false, null, 0);
        }
        var paid = false;
        if(installment.StatementId is Guid statementId) {
            paid = await context.MonthlyStatements.AnyAsync(statement => statement.Id == statementId && statement.PaidOnUtc != null, cancellationToken);
        }
        return new InstallmentStatusResponse(
            true,
            installment.IsAccrued,
            paid,
            installment.StatementId,
            installment.Amount.MinorUnits
        );
    }
}

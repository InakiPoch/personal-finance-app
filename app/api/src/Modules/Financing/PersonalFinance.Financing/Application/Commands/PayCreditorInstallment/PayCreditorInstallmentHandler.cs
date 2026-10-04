using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Application.Commands.PayCreditorInstallment;

/// <summary>
/// Records a payment (full or partial) against one creditor-financed installment — a display-only
/// <see cref="Installment.ApplyPayment"/> row. No bank account, no ledger posting — this is the mirror
/// of the card path's <c>InstallmentNotAccrued</c> guard from the other side: an installment on a
/// credit-card plan is rejected here with <see cref="FinancingErrors.NotACreditorInstallment"/>.
/// </summary>
internal sealed class PayCreditorInstallmentHandler(FinancingDbContext context, TimeProvider timeProvider) : ICommandHandler<PayCreditorInstallmentCommand, Guid> {
    public async Task<Result<Guid>> HandleAsync(PayCreditorInstallmentCommand command, CancellationToken cancellationToken) {
        var validation = PayCreditorInstallmentValidator.Validate(command);
        if(validation.IsFailure) {
            return validation.Error;
        }
        var installment = await context.Set<Installment>()
            .Include(candidate => candidate.Payments)
            .FirstOrDefaultAsync(candidate => candidate.Id == command.InstallmentId, cancellationToken);
        if(installment is null) {
            return FinancingErrors.InstallmentNotFound;
        }
        var plan = await context.PaymentPlans
            .FirstOrDefaultAsync(candidate => candidate.Id == installment.PaymentPlanId, cancellationToken);
        if(plan is null || plan.CreditorId is null) {
            return FinancingErrors.NotACreditorInstallment;
        }
        var applied = installment.ApplyPayment(command.AmountMinorUnits ?? installment.RemainingMinorUnits, timeProvider.GetUtcNow());
        if(applied.IsFailure) {
            return applied.Error;
        }
        await context.SaveChangesAsync(cancellationToken);
        return installment.Id;
    }
}

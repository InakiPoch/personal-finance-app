using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Application.Commands.UnpayCreditorInstallment;

/// <summary>
/// Reverts a creditor installment's display-only paid stamp. There is no ledger transaction to storno,
/// so this just clears <see cref="Installment.PaidOnUtc"/>; clearing an already-unpaid installment is a
/// harmless no-op. An installment on a credit-card plan is rejected with
/// <see cref="FinancingErrors.NotACreditorInstallment"/>.
/// </summary>
internal sealed class UnpayCreditorInstallmentHandler(FinancingDbContext context)
    : ICommandHandler<UnpayCreditorInstallmentCommand, Guid> {
    public async Task<Result<Guid>> HandleAsync(UnpayCreditorInstallmentCommand command, CancellationToken cancellationToken) {
        var validation = UnpayCreditorInstallmentValidator.Validate(command);
        if(validation.IsFailure) {
            return validation.Error;
        }
        var installment = await context.Set<Installment>()
            .FirstOrDefaultAsync(candidate => candidate.Id == command.InstallmentId, cancellationToken);
        if(installment is null) {
            return FinancingErrors.InstallmentNotFound;
        }
        var plan = await context.PaymentPlans
            .FirstOrDefaultAsync(candidate => candidate.Id == installment.PaymentPlanId, cancellationToken);
        if(plan is null || plan.CreditorId is null) {
            return FinancingErrors.NotACreditorInstallment;
        }
        if(installment.IsReversed) {
            return FinancingErrors.InstallmentAlreadyReversed;
        }
        var cleared = installment.ClearPayment();
        if(cleared.IsFailure) {
            return cleared.Error;
        }
        await context.SaveChangesAsync(cancellationToken);
        return installment.Id;
    }
}

using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Application.Commands.PayCreditorFullDebt;

/// <summary>
/// Settles a creditor's entire remaining debt in one transaction: a display-only payment for the full
/// <see cref="Installment.RemainingMinorUnits"/> of every unpaid, non-reversed installment across all of
/// that creditor's purchases. No bank account, no ledger posting.
/// </summary>
internal sealed class PayCreditorFullDebtHandler(FinancingDbContext context, TimeProvider timeProvider) : ICommandHandler<PayCreditorFullDebtCommand, int> {
    public async Task<Result<int>> HandleAsync(PayCreditorFullDebtCommand command, CancellationToken cancellationToken) {
        var validation = PayCreditorFullDebtValidator.Validate(command);
        if(validation.IsFailure) {
            return validation.Error;
        }
        var creditorExists = await context.Creditors
            .AnyAsync(candidate => candidate.Id == command.CreditorId, cancellationToken);
        if(creditorExists == false) {
            return FinancingErrors.CreditorNotFound;
        }
        var installments = await (
            from installment in context.Set<Installment>().Include(candidate => candidate.Payments)
            join plan in context.PaymentPlans on installment.PaymentPlanId equals plan.Id
            where plan.CreditorId == command.CreditorId
            select installment
        ).ToListAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var settled = 0;
        foreach(var installment in installments) {
            if(installment.IsReversed || installment.RemainingMinorUnits == 0) {
                continue;
            }
            var applied = installment.ApplyPayment(installment.RemainingMinorUnits, now);
            if(applied.IsFailure) {
                return applied.Error;
            }
            settled++;
        }
        await context.SaveChangesAsync(cancellationToken);
        return settled;
    }
}

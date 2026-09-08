using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Application.Commands.PayCreditorFullDebt;

/// <summary>
/// Settles a creditor's entire remaining debt in one transaction: a display-only
/// <see cref="Installment.PaidOnUtc"/> stamp (the server clock) on every unpaid, non-reversed installment
/// across all of that creditor's purchases. No bank account, no ledger posting. Already-paid and reversed
/// installments are skipped; the returned count reflects only the newly-settled ones, so running it twice
/// settles zero the second time (idempotent — not an error).
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
            from installment in context.Set<Installment>()
            join plan in context.PaymentPlans on installment.PaymentPlanId equals plan.Id
            where plan.CreditorId == command.CreditorId
            select installment
        ).ToListAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var settled = 0;
        foreach(var installment in installments) {
            if(installment.IsReversed || installment.IsPaid) {
                continue;
            }
            var marked = installment.MarkPaid(now);
            if(marked.IsFailure) {
                return marked.Error;
            }
            settled++;
        }
        await context.SaveChangesAsync(cancellationToken);
        return settled;
    }
}

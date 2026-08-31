using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Application.Commands.LinkPaymentPlanSplit;

internal sealed class LinkPaymentPlanSplitHandler(FinancingDbContext context) : ICommandHandler<LinkPaymentPlanSplitCommand> {
    public async Task<Result> HandleAsync(LinkPaymentPlanSplitCommand command, CancellationToken cancellationToken) {
        var validation = LinkPaymentPlanSplitValidator.Validate(command);
        if(validation.IsFailure) {
            return validation;
        }
        var plan = await context.PaymentPlans
            .Include(candidate => candidate.SplitParticipants)
            .FirstOrDefaultAsync(candidate => candidate.Id == command.PaymentPlanId, cancellationToken);
        if(plan is null) {
            return Result.Failure(FinancingErrors.PaymentPlanNotFound);
        }
        if(plan.SplitReferenceId is not null) {
            return Result.Success();
        }
        var receivableAccountsByParty = command.PartyReceivables
            .ToDictionary(receivable => receivable.PartyId, receivable => receivable.ReceivableAccountId);
        var linked = plan.LinkSplit(command.SplitReferenceId, receivableAccountsByParty);
        if(linked.IsFailure) {
            return linked;
        }
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.SharedKernel;
using PersonalFinance.Subscriptions.Contracts.Commands;
using PersonalFinance.Subscriptions.Domain;
using PersonalFinance.Subscriptions.Infrastructure.Persistence;

namespace PersonalFinance.Subscriptions.Application.Commands.PaySubscription;

internal sealed class PaySubscriptionHandler(SubscriptionsDbContext context, ILedgerApi ledger, TimeProvider timeProvider) : ICommandHandler<PaySubscriptionCommand, Guid> {
    public async Task<Result<Guid>> HandleAsync(PaySubscriptionCommand command, CancellationToken cancellationToken) {
        var validation = PaySubscriptionValidator.Validate(command);
        if(validation.IsFailure) {
            return validation.Error;
        }
        var template = await context.SubscriptionTemplates
            .FirstOrDefaultAsync(candidate => candidate.Id == command.SubscriptionId, cancellationToken);
        if(template is null) {
            return SubscriptionErrors.SubscriptionNotFound;
        }
        if(!template.IsActive) {
            return SubscriptionErrors.SubscriptionNotActive;
        }
        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        if(template.LastPaidPeriod is { } paidPeriod && paidPeriod.Year == today.Year && paidPeriod.Month == today.Month) {
            return SubscriptionErrors.SubscriptionAlreadyPaid;
        }
        var paidPeriodAnchor = template.NextDueDate;
        var posting = await ledger.PostTransactionAsync(
            SubscriptionChargeCalculator.Build(template.ExpenseAccountId, template.FundingAccountId, template.Amount, template.Id, now),
            cancellationToken
        );
        if(posting.IsFailure) {
            return posting.Error;
        }
        var marked = template.MarkCurrentPeriodPaid(paidPeriodAnchor);
        if(marked.IsFailure) {
            return marked.Error;
        }
        await context.SaveChangesAsync(cancellationToken);
        return template.Id;
    }
}

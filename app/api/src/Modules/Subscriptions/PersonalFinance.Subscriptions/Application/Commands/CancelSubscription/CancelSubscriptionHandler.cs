using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.SharedKernel;
using PersonalFinance.Subscriptions.Contracts.Commands;
using PersonalFinance.Subscriptions.Domain;
using PersonalFinance.Subscriptions.Infrastructure.Persistence;

namespace PersonalFinance.Subscriptions.Application.Commands.CancelSubscription;

internal sealed class CancelSubscriptionHandler(SubscriptionsDbContext context) : ICommandHandler<CancelSubscriptionCommand> {
    public async Task<Result> HandleAsync(CancelSubscriptionCommand command, CancellationToken cancellationToken) {
        var validation = CancelSubscriptionValidator.Validate(command);
        if(validation.IsFailure) {
            return validation;
        }
        var template = await context.SubscriptionTemplates
            .FirstOrDefaultAsync(candidate => candidate.Id == command.SubscriptionId, cancellationToken);
        if(template is null) {
            return Result.Failure(SubscriptionErrors.SubscriptionNotFound);
        }
        var cancelled = template.Cancel();
        if(cancelled.IsFailure) {
            return cancelled;
        }
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.SharedKernel;
using PersonalFinance.Subscriptions.Contracts.Commands;
using PersonalFinance.Subscriptions.Domain;
using PersonalFinance.Subscriptions.Infrastructure.Persistence;

namespace PersonalFinance.Subscriptions.Application.Commands.UnpaySubscription;

internal sealed class UnpaySubscriptionHandler(SubscriptionsDbContext context, ILedgerApi ledger, TimeProvider timeProvider) : ICommandHandler<UnpaySubscriptionCommand, Guid> {
    public async Task<Result<Guid>> HandleAsync(UnpaySubscriptionCommand command, CancellationToken cancellationToken) {
        var validation = UnpaySubscriptionValidator.Validate(command);
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
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        if(template.LastPaidPeriod is not { } paidPeriod || paidPeriod.Year != today.Year || paidPeriod.Month != today.Month) {
            return SubscriptionErrors.SubscriptionNotPaid;
        }
        var now = timeProvider.GetUtcNow();
        var reversal = await ledger.ReverseTransactionAsync(
            new ReverseTransactionCommand(template.LastPaidTransactionId ?? Guid.Empty, now),
            cancellationToken
        );
        if(reversal.IsFailure) {
            return reversal.Error;
        }
        var reverted = template.RevertLastPayment();
        if(reverted.IsFailure) {
            return reverted.Error;
        }
        await context.SaveChangesAsync(cancellationToken);
        return template.Id;
    }
}

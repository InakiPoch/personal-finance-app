using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.SharedKernel;
using PersonalFinance.Subscriptions.Contracts.Commands;
using PersonalFinance.Subscriptions.Domain;
using PersonalFinance.Subscriptions.Infrastructure.Persistence;

namespace PersonalFinance.Subscriptions.Application.Commands.RenewSubscription;

internal sealed class RenewSubscriptionHandler(SubscriptionsDbContext context, ILedgerApi ledger) : ICommandHandler<RenewSubscriptionCommand, Guid> {
    public async Task<Result<Guid>> HandleAsync(RenewSubscriptionCommand command, CancellationToken cancellationToken) {
        var validation = RenewSubscriptionValidator.Validate(command);
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
        var posting = await ledger.PostTransactionAsync(
            SubscriptionChargeCalculator.Build(
                template.ExpenseAccountId,
                template.FundingAccountId,
                template.Amount,
                template.Id,
                command.RenewedOnUtc
            ),
            cancellationToken
        );
        if(posting.IsFailure) {
            return posting.Error;
        }
        var renewed = template.Renew(command.RenewedOnUtc);
        if(renewed.IsFailure) {
            return renewed.Error;
        }
        await context.SaveChangesAsync(cancellationToken);
        return posting.Value;
    }
}

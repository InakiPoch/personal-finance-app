using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Contracts.IntegrationEvents;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.Financing.Infrastructure.Persistence.Outbox;
using PersonalFinance.SharedKernel;
using PersonalFinance.SharedKernel.Allocation;

namespace PersonalFinance.Financing.Application.Commands.CreatePaymentPlan;

/// <summary>
/// Persists an installment plan and, when the purchase is shared, a durable <see cref="PaymentPlanCreatedIntegrationEvent"/>.
/// </summary>
internal sealed class CreatePaymentPlanHandler(FinancingDbContext context, FinancingOutboxWriter outboxWriter) : ICommandHandler<CreatePaymentPlanCommand, Guid> {
    public async Task<Result<Guid>> HandleAsync(CreatePaymentPlanCommand command, CancellationToken cancellationToken) {
        var validation = CreatePaymentPlanValidator.Validate(command);
        if(validation.IsFailure) {
            return validation.Error;
        }
        int? cutoffDay;
        if(command.CardId is { } cardId) {
            var card = await context.CreditCards.FirstOrDefaultAsync(candidate => candidate.Id == cardId, cancellationToken);
            if(card is null) {
                return FinancingErrors.CardNotFound;
            }
            cutoffDay = card.CutoffDay;
        } else {
            var creditor = await context.Creditors
                .Include(candidate => candidate.Accounts)
                .FirstOrDefaultAsync(candidate => candidate.Id == command.CreditorId, cancellationToken);
            if(creditor is null) {
                return FinancingErrors.CreditorNotFound;
            }
            if(creditor.Accounts.All(account => account.Id != command.CreditorAccountId)) {
                return FinancingErrors.CreditorAccountMismatch;
            }
            cutoffDay = null;
        }
        var plan = PaymentPlan.Create(
            command.CardId,
            Money.FromMinorUnits(command.AmountMinorUnits, Currency.Reference),
            command.InstallmentCount,
            command.PurchaseDate,
            command.Description,
            cutoffDay,
            new PhantomPennyAllocator(),
            command.Split?.Participants.Select(participant => (participant.PartyId, participant.Weight)).ToList(),
            command.CreditorId,
            command.CreditorAccountId
        );
        if(plan.IsFailure) {
            return plan.Error;
        }
        context.PaymentPlans.Add(plan.Value);
        if(command.Split is not null) {
            outboxWriter.Add(new PaymentPlanCreatedIntegrationEvent(
                Guid.CreateVersion7(),
                DateTimeOffset.UtcNow,
                plan.Value.Id,
                command.CardId,
                plan.Value.Total.MinorUnits,
                plan.Value.PurchaseDate,
                command.Split.Participants)
            );
        }
        await context.SaveChangesAsync(cancellationToken);
        return plan.Value.Id;
    }
}

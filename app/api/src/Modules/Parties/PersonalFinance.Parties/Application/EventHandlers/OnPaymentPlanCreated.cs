using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Contracts.IntegrationEvents;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Parties.Domain;
using PersonalFinance.Parties.Infrastructure.Persistence;
using PersonalFinance.Parties.Infrastructure.Persistence.Inbox;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Parties.Application.EventHandlers;

internal sealed class OnPaymentPlanCreated(
    PartiesDbContext context,
    ILedgerApi ledger,
    IFinancingApi financing,
    PartiesInboxStore inbox) : IIntegrationEventHandler<PaymentPlanCreatedIntegrationEvent> {
    private const string Consumer = "Parties";

    public async Task HandleAsync(PaymentPlanCreatedIntegrationEvent integrationEvent, CancellationToken cancellationToken) {
        if(await inbox.IsConsumedAsync(integrationEvent.MessageId, Consumer, cancellationToken)) {
            return;
        }
        var participantIds = integrationEvent.Participants.Select(participant => participant.PartyId).ToList();
        var knownParties = await context.Parties
            .Where(party => participantIds.Contains(party.Id))
            .ToDictionaryAsync(party => party.Id, cancellationToken);
        var receivables = new List<PartyReceivable>(participantIds.Count);
        foreach(var participant in integrationEvent.Participants) {
            if(knownParties.TryGetValue(participant.PartyId, out var known)) {
                receivables.Add(new PartyReceivable(known.Id, known.ReceivableAccountId));
                continue;
            }
            var account = await ledger.CreateAccountAsync(
                new CreateAccountCommand($"Party {participant.PartyId} Receivable", AccountType.Asset, AccountKind.Receivable),
                cancellationToken);
            if(account.IsFailure) {
                throw new InvalidOperationException(
                    $"Could not provision a receivable account for party {participant.PartyId}: {account.Error.Code}.");
            }
            var placeholder = Party.Placeholder(participant.PartyId, account.Value);
            context.Parties.Add(placeholder);
            receivables.Add(new PartyReceivable(placeholder.Id, account.Value));
        }
        var currency = Currency.Reference;
        var weights = integrationEvent.Participants.Select(participant => participant.Weight).ToList();
        var shares = SplitAllocationCalculator.AllocateWhole(integrationEvent.TotalMinorUnits, weights);
        var holderShare = Money.FromMinorUnits(shares.HolderShare, currency);
        var participantShares = integrationEvent.Participants
            .Select((participant, index) => new PartyShare(participant.PartyId, Money.FromMinorUnits(shares.ParticipantShares[index], currency)))
            .ToList();
        var total = Money.FromMinorUnits(integrationEvent.TotalMinorUnits, currency);
        var split = ExpenseSplit.Create(
            ExpenseSplitSource.CardPlan,
            integrationEvent.PaymentPlanId,
            total,
            holderShare,
            participantShares,
            Money.Zero(currency)
        );
        if(split.IsFailure) {
            throw new InvalidOperationException(
                $"Could not build the expense split for payment plan {integrationEvent.PaymentPlanId}: {split.Error.Code}.");
        }
        context.ExpenseSplits.Add(split.Value);
        var link = await financing.LinkSplitAsync(
            new LinkPaymentPlanSplitCommand(integrationEvent.PaymentPlanId, split.Value.Id, receivables),
            cancellationToken
        );
        if(link.IsFailure) {
            throw new InvalidOperationException(
                $"Could not link payment plan {integrationEvent.PaymentPlanId} to split {split.Value.Id}: {link.Error.Code}.");
        }
        await inbox.MarkConsumedAsync(integrationEvent.MessageId, Consumer, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }
}

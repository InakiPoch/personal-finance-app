using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.Ledger.Contracts.IntegrationEvents;
using PersonalFinance.Ledger.Domain;
using PersonalFinance.Ledger.Domain.Events;
using PersonalFinance.Ledger.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Ledger.Application;

/// <summary>
/// The single persistence path for a <see cref="Transaction"/>: saves it, translates each
/// <see cref="TransactionPosted"/> domain event into a <see cref="TransactionPostedIntegrationEvent"/>,
/// dispatches it, then clears the aggregate's events.
/// </summary>
internal sealed class TransactionWriter(LedgerDbContext context, IIntegrationEventDispatcher dispatcher) {
    public async Task<Result<Guid>> PersistAsync(Transaction transaction, CancellationToken cancellationToken) {
        context.Transactions.Add(transaction);
        await context.SaveChangesAsync(cancellationToken);
        foreach(var domainEvent in transaction.DomainEvents.OfType<TransactionPosted>()) {
            var integrationEvent = new TransactionPostedIntegrationEvent(
                Guid.CreateVersion7(),
                DateTimeOffset.UtcNow,
                domainEvent.TransactionId,
                domainEvent.IsReversal,
                transaction.OriginalTransactionId);
            await dispatcher.DispatchAsync(integrationEvent, cancellationToken);
        }
        transaction.ClearDomainEvents();
        return transaction.Id;
    }
}

using PersonalFinance.Infrastructure.Outbox;

namespace PersonalFinance.Subscriptions.Infrastructure.Persistence.Outbox;

internal sealed class SubscriptionsOutboxWriter(SubscriptionsDbContext context) : OutboxWriterBase {
    protected override void AddToUnitOfWork(OutboxMessage message) {
        context.Set<OutboxMessage>().Add(message);
    }
}

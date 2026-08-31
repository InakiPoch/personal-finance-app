using PersonalFinance.Infrastructure.Outbox;

namespace PersonalFinance.Parties.Infrastructure.Persistence.Outbox;

internal sealed class PartiesOutboxWriter(PartiesDbContext context) : OutboxWriterBase {
    protected override void AddToUnitOfWork(OutboxMessage message) {
        context.Set<OutboxMessage>().Add(message);
    }
}

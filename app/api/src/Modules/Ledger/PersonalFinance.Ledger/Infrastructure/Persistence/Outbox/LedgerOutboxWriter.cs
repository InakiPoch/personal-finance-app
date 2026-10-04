using PersonalFinance.Infrastructure.Outbox;

namespace PersonalFinance.Ledger.Infrastructure.Persistence.Outbox;

/// <summary>
/// Enlists serialized integration events into the Ledger unit of work.
/// </summary>
internal sealed class LedgerOutboxWriter(LedgerDbContext context) : OutboxWriterBase {
    protected override void AddToUnitOfWork(OutboxMessage message) {
        context.Set<OutboxMessage>().Add(message);
    }
}

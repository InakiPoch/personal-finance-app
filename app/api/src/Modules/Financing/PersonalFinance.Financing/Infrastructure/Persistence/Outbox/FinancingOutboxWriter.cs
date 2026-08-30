using PersonalFinance.Infrastructure.Outbox;

namespace PersonalFinance.Financing.Infrastructure.Persistence.Outbox;

internal sealed class FinancingOutboxWriter(FinancingDbContext context) : OutboxWriterBase {
    protected override void AddToUnitOfWork(OutboxMessage message) {
        context.Set<OutboxMessage>().Add(message);
    }
}

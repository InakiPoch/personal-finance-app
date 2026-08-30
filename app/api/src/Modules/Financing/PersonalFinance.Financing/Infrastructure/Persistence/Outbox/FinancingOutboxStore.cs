using Microsoft.EntityFrameworkCore;
using PersonalFinance.Infrastructure.Outbox;

namespace PersonalFinance.Financing.Infrastructure.Persistence.Outbox;

internal sealed class FinancingOutboxStore(FinancingDbContext context) : IOutboxStore {
    public string ModuleName => "Financing";

    public async Task<IReadOnlyList<OutboxMessage>> GetUnprocessedBatchAsync(int batchSize, CancellationToken cancellationToken) {
        return await context.Set<OutboxMessage>()
            .Where(message => message.ProcessedOnUtc == null)
            .OrderBy(message => message.Id)
            .Take(batchSize)
        .ToListAsync(cancellationToken);
    }

    public async Task MarkProcessedAsync(long id, CancellationToken cancellationToken) {
        var message = await context.Set<OutboxMessage>().FindAsync([id], cancellationToken);
        if(message is null) return;
        message.ProcessedOnUtc = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(long id, string error, CancellationToken cancellationToken) {
        var message = await context.Set<OutboxMessage>().FindAsync([id], cancellationToken);
        if(message is null) return;
        message.Attempts += 1;
        message.Error = error;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<OutboxBacklog> GetBacklogAsync(CancellationToken cancellationToken) {
        var pending = context.Set<OutboxMessage>().Where(message => message.ProcessedOnUtc == null);
        var count = await pending.CountAsync(cancellationToken);
        var oldest = count == 0
            ? null
            : await pending
                .OrderBy(message => message.Id)
                .Select(message => (DateTimeOffset?)message.OccurredOnUtc)
                .FirstOrDefaultAsync(cancellationToken);
        return new OutboxBacklog(count, oldest);
    }
}

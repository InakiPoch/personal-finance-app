using Microsoft.EntityFrameworkCore;

namespace PersonalFinance.Infrastructure.Idempotency;

/// <summary>
/// Deduplicates integration-event delivery per consumer.
/// </summary>
public interface IInboxStore {
    Task<bool> IsConsumedAsync(Guid messageId, string consumer, CancellationToken cancellationToken);
    Task MarkConsumedAsync(Guid messageId, string consumer, CancellationToken cancellationToken);
}

/// <summary>
/// Reads and writes <see cref="InboxConsumedMessage"/> rows against the consumer's <see cref="DbContext"/>.
/// </summary>
public abstract class InboxStoreBase(DbContext context) : IInboxStore {
    public Task<bool> IsConsumedAsync(Guid messageId, string consumer, CancellationToken cancellationToken) {
        return context.Set<InboxConsumedMessage>()
            .AnyAsync(row => row.MessageId == messageId && row.Consumer == consumer, cancellationToken);
    }

    public Task MarkConsumedAsync(Guid messageId, string consumer, CancellationToken cancellationToken) {
        context.Set<InboxConsumedMessage>().Add(new InboxConsumedMessage {
            MessageId = messageId,
            Consumer = consumer,
            ConsumedOnUtc = DateTimeOffset.UtcNow
        });
        return Task.CompletedTask;
    }
}

namespace PersonalFinance.Infrastructure.Outbox;

/// <summary>
/// One module's view of its <c>&lt;module&gt;_outbox_messages</c> table.
/// </summary>
public interface IOutboxStore {
    string ModuleName { get; }
    Task<IReadOnlyList<OutboxMessage>> GetUnprocessedBatchAsync(int batchSize, CancellationToken cancellationToken);
    Task MarkProcessedAsync(long id, CancellationToken cancellationToken);
    Task MarkFailedAsync(long id, string error, CancellationToken cancellationToken);
    Task<OutboxBacklog> GetBacklogAsync(CancellationToken cancellationToken);
}

public readonly record struct OutboxBacklog(int PendingCount, DateTimeOffset? OldestUnprocessedOccurredOnUtc);
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace PersonalFinance.Infrastructure.Outbox;

/// <summary>
/// Reports the outbox backlog across every module. Unhealthy once a message has sat unprocessed
/// longer than <see cref="OutboxOptions.StalenessThreshold"/>
/// </summary>
public sealed class OutboxHealthCheck(IEnumerable<IOutboxStore> stores, IOptions<OutboxOptions> options, TimeProvider timeProvider) : IHealthCheck {
    private readonly OutboxOptions options = options.Value;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) {
        var totalPending = 0;
        DateTimeOffset? oldest = null;
        foreach(var store in stores) {
            var backlog = await store.GetBacklogAsync(cancellationToken);
            totalPending += backlog.PendingCount;
            if(backlog.OldestUnprocessedOccurredOnUtc is { } occurred && (oldest is null || occurred < oldest)) {
                oldest = occurred;
            }
        }
        if(totalPending == 0) {
            return HealthCheckResult.Healthy("Outbox is clear.");
        }
        var age = oldest is { } value ? timeProvider.GetUtcNow() - value : TimeSpan.Zero;
        var data = new Dictionary<string, object> {
            ["pending"] = totalPending,
            ["oldestAgeSeconds"] = age.TotalSeconds,
        };
        return age > options.StalenessThreshold
            ? HealthCheckResult.Unhealthy($"Outbox backlog stale: {totalPending} pending, oldest {age.TotalSeconds:F0}s old.", data: data)
        : HealthCheckResult.Degraded($"Outbox backlog: {totalPending} pending, oldest {age.TotalSeconds:F0}s old.", data: data);
    }
}

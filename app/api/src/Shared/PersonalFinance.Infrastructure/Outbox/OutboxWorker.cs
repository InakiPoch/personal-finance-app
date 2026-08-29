using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Infrastructure.Messaging;

namespace PersonalFinance.Infrastructure.Outbox;

/// <summary>
/// Drains every module's outbox on a timer: for each unprocessed <see cref="OutboxMessage"/> it
/// rebuilds the <see cref="IIntegrationEvent"/> and hands it to
/// <see cref="IIntegrationEventDispatcher"/> (the durable dispatch path, D8) processed.
/// </summary>
public sealed class OutboxWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options,
    ILogger<OutboxWorker> logger) : BackgroundService {
    private readonly OutboxOptions options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        using var timer = new PeriodicTimer(options.PollingInterval);
        try {
            do {
                await DrainAllAsync(stoppingToken);
            } while(await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch(OperationCanceledException) {
            // Host is stopping.
        }
    }

    private async Task DrainAllAsync(CancellationToken cancellationToken) {
        try {
            using var scope = scopeFactory.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IIntegrationEventDispatcher>();
            foreach(var store in scope.ServiceProvider.GetServices<IOutboxStore>()) {
                await DrainStoreAsync(store, dispatcher, cancellationToken);
            }
        }
        catch(Exception ex) when(ex is not OperationCanceledException) {
            logger.LogError(ex, "Outbox drain pass failed; retrying on the next tick.");
        }
    }

    private async Task DrainStoreAsync(IOutboxStore store, IIntegrationEventDispatcher dispatcher, CancellationToken cancellationToken) {
        var batch = await store.GetUnprocessedBatchAsync(options.BatchSize, cancellationToken);
        foreach(var message in batch) {
            try {
                var eventType = Type.GetType(message.Type, throwOnError: true)!;
                var integrationEvent = (IIntegrationEvent)JsonSerializer.Deserialize(message.Payload, eventType)!;
                await dispatcher.DispatchAsync(integrationEvent, cancellationToken);
                await store.MarkProcessedAsync(message.Id, cancellationToken);
            }
            catch(Exception ex) when(ex is not OperationCanceledException) {
                logger.LogError(ex, "Outbox message {MessageId} ({Type}) from {Module} failed; will retry.",
                    message.MessageId, message.Type, store.ModuleName);
                await store.MarkFailedAsync(message.Id, ex.ToString(), cancellationToken);
            }
        }
    }
}

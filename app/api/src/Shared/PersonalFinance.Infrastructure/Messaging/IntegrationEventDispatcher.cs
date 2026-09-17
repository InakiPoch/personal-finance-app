using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Infrastructure.Messaging;

/// <summary>
/// Fans an <see cref="IIntegrationEvent"/> out to every registered
/// <see cref="IIntegrationEventHandler{TEvent}"/> in the process, resolved by the event's
/// runtime type and invoked sequentially. Two callers use it:
/// <list type="bullet">
///   <item>
///     <b>Durable</b> — the <c>OutboxWorker</c> drains a module's <c>*_outbox_messages</c> and
///     calls this per row.
///   </item>
///   <item>
///     <b>Direct</b> — a scheduler (<c>AccrueInstallments</c>) calls this synchronously right
///     after its own transaction commits.
///   </item>
/// </list>
/// </summary>
public interface IIntegrationEventDispatcher {
    Task DispatchAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default);
}

public sealed class IntegrationEventDispatcher(IServiceProvider serviceProvider) : IIntegrationEventDispatcher {
    public async Task DispatchAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        var handlerType = typeof(IIntegrationEventHandler<>).MakeGenericType(integrationEvent.GetType());
        var method = handlerType.GetMethod("HandleAsync")
            ?? throw new InvalidOperationException($"{handlerType} has no HandleAsync method.");
        foreach(var handler in serviceProvider.GetServices(handlerType)) {
            if(handler is null) {
                continue;
            }
            var task = (Task)(method.Invoke(handler, [integrationEvent, cancellationToken])
                ?? throw new InvalidOperationException($"{handlerType}.HandleAsync returned null."));
            await task.ConfigureAwait(false);
        }
    }
}

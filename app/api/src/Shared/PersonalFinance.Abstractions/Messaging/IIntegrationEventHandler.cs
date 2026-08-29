namespace PersonalFinance.Abstractions.Messaging;

/// <summary>
/// Reacts to a single <typeparamref name="TEvent"/>.
/// </summary>
/// <typeparam name="TEvent">The integration event this handler consumes.</typeparam>
public interface IIntegrationEventHandler<in TEvent> where TEvent : IIntegrationEvent {
    Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken);
}

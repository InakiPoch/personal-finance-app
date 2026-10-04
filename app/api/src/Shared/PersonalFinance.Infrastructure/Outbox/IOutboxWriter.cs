using System.Text.Json;
using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Infrastructure.Outbox;

/// <summary>
/// Enlists an <see cref="IIntegrationEvent"/> into the current module's unit of work.
/// </summary>
public interface IOutboxWriter {
    void Add(IIntegrationEvent integrationEvent);
}

/// <summary>
/// Serializes the event to an <see cref="OutboxMessage"/>.
/// </summary>
public abstract class OutboxWriterBase : IOutboxWriter {
    public void Add(IIntegrationEvent integrationEvent) {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        var type = integrationEvent.GetType();
        AddToUnitOfWork(new OutboxMessage {
            MessageId = integrationEvent.MessageId,
            Type = type.AssemblyQualifiedName ?? type.FullName ?? type.Name,
            Payload = JsonSerializer.Serialize(integrationEvent, type),
            OccurredOnUtc = integrationEvent.OccurredOnUtc
        });
    }

    protected abstract void AddToUnitOfWork(OutboxMessage message);
}

namespace PersonalFinance.Abstractions.Messaging;

/// <summary>
/// A fact one module publishes for others to react to.
/// </summary>
public interface IIntegrationEvent {
    Guid MessageId { get; }
    DateTimeOffset OccurredOnUtc { get; }
}

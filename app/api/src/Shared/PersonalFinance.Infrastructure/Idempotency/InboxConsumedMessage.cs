namespace PersonalFinance.Infrastructure.Idempotency;

/// <summary>
/// One integration event already handled by one consumer. Composite key: <see cref="MessageId"/> + <see cref="Consumer"/>.
/// </summary>
public sealed class InboxConsumedMessage {
    public Guid MessageId { get; set; }
    public string Consumer { get; set; } = string.Empty;
    public DateTimeOffset ConsumedOnUtc { get; set; }
}

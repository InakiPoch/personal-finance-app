namespace PersonalFinance.Infrastructure.Outbox;

/// <summary>
/// One integration event awaiting delivery.
/// </summary>
public sealed class OutboxMessage {
    public long Id { get; set; }
    public Guid MessageId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTimeOffset OccurredOnUtc { get; set; }
    public DateTimeOffset? ProcessedOnUtc { get; set; }
    public int Attempts { get; set; }
    public string? Error { get; set; }
}

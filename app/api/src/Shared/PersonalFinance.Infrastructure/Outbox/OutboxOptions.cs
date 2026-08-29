namespace PersonalFinance.Infrastructure.Outbox;

/// <summary>
/// Binds the <c>Outbox</c> configuration section; tunes the <see cref="OutboxWorker"/>.
/// </summary>
public sealed class OutboxOptions {
    public const string SectionName = "Outbox";

    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan StalenessThreshold { get; set; } = TimeSpan.FromMinutes(5);
    public int BatchSize { get; set; } = 100;
}

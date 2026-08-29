namespace PersonalFinance.Infrastructure.Persistence;

/// <summary>
/// Binds the <c>Sqlite</c> configuration section.
/// </summary>
public sealed class SqliteOptions {
    public const string SectionName = "Sqlite";

    public string ConnectionString { get; set; } = string.Empty;
    public int BusyTimeoutMs { get; set; } = 5000;
    public string JournalMode { get; set; } = "WAL";
    public bool ForeignKeys { get; set; } = true;
}

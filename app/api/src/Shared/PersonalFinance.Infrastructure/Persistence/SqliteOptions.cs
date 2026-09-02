using System.ComponentModel.DataAnnotations;

namespace PersonalFinance.Infrastructure.Persistence;

public sealed class SqliteOptions {
    public const string SectionName = "Sqlite";
    [Required]
    public string ConnectionString { get; set; } = string.Empty;
    [Range(0, int.MaxValue)]
    public int BusyTimeoutMs { get; set; } = 5000;
    [Required]
    public string JournalMode { get; set; } = "WAL";
    public bool ForeignKeys { get; set; } = true;
}

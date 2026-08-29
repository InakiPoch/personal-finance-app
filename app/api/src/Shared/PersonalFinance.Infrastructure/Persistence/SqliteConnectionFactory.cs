using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace PersonalFinance.Infrastructure.Persistence;

/// <summary>
/// Hands out <see cref="SqliteConnection"/>s already opened and configured with the pragmas every writer in the process needs 
/// </summary>
public interface ISqliteConnectionFactory {
    SqliteConnection CreateOpenConnection();
}

public sealed class SqliteConnectionFactory(IOptions<SqliteOptions> optionsAccessor) : ISqliteConnectionFactory {
    private readonly SqliteOptions options = optionsAccessor.Value;

    public SqliteConnection CreateOpenConnection() {
        var connection = new SqliteConnection(options.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            $"PRAGMA journal_mode = {options.JournalMode};" +
            $"PRAGMA busy_timeout = {options.BusyTimeoutMs};" +
            $"PRAGMA foreign_keys = {(options.ForeignKeys ? "ON" : "OFF")};";
        command.ExecuteNonQuery();
        return connection;
    }
}

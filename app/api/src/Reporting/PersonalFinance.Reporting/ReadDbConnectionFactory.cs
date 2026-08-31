using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Reporting;

/// <summary>
/// Hands out a read-only <see cref="SqliteConnection"/> onto the one shared database.
/// </summary>
internal interface IReadDbConnectionFactory {
    SqliteConnection CreateOpenConnection();
}

internal sealed class ReadDbConnectionFactory(IOptions<SqliteOptions> optionsAccessor) : IReadDbConnectionFactory {
    private readonly SqliteOptions options = optionsAccessor.Value;

    public SqliteConnection CreateOpenConnection() {
        var readOnlyConnectionString = new SqliteConnectionStringBuilder(options.ConnectionString) {
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();
        var connection = new SqliteConnection(readOnlyConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA busy_timeout = {options.BusyTimeoutMs};";
        command.ExecuteNonQuery();
        return connection;
    }
}

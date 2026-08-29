using Microsoft.Data.Sqlite;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Ledger.Infrastructure.Persistence;

/// <summary>
/// Guard handed to <see cref="LedgerDbContext"/> at design time. The design-time factory configures the
/// context with an explicit connection string,
/// </summary>
internal sealed class DesignTimeSqliteConnectionFactory : ISqliteConnectionFactory {
    public SqliteConnection CreateOpenConnection() {
        throw new InvalidOperationException(
            "DesignTimeSqliteConnectionFactory must not create connections. The design-time context is " +
            "configured with an explicit connection string."
        );
    }
}

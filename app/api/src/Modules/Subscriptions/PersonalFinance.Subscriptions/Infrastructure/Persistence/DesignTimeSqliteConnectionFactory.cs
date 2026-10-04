using Microsoft.Data.Sqlite;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Subscriptions.Infrastructure.Persistence;

/// <summary>
/// Guard handed to <see cref="SubscriptionsDbContext"/> at design time.
/// </summary>
internal sealed class DesignTimeSqliteConnectionFactory : ISqliteConnectionFactory {
    public SqliteConnection CreateOpenConnection() {
        throw new InvalidOperationException(
            "DesignTimeSqliteConnectionFactory must not create connections. The design-time context is " +
            "configured with an explicit connection string."
        );
    }
}

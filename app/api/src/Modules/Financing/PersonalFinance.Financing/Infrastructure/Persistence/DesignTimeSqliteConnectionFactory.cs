using Microsoft.Data.Sqlite;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Financing.Infrastructure.Persistence;

/// <summary>
/// Guard handed to <see cref="FinancingDbContext"/> at design time.
/// </summary>
internal sealed class DesignTimeSqliteConnectionFactory : ISqliteConnectionFactory {
    public SqliteConnection CreateOpenConnection() {
        throw new InvalidOperationException(
            "DesignTimeSqliteConnectionFactory must not create connections. The design-time context is " +
            "configured with an explicit connection string."
        );
    }
}

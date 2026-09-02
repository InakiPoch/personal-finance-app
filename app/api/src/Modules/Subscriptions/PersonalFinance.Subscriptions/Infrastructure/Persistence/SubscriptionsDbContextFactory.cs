using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Subscriptions.Infrastructure.Persistence;

internal sealed class SubscriptionsDbContextFactory : IDesignTimeDbContextFactory<SubscriptionsDbContext> {
    public SubscriptionsDbContext CreateDbContext(string[] args) {
        var connectionString = SqliteConnectionStringHelper.ResolveForDesignTime();
        var options = new DbContextOptionsBuilder<SubscriptionsDbContext>()
            .UseSqlite(connectionString, sqlite => sqlite.MigrationsHistoryTable("__EFMigrationsHistory_Subscriptions"))
            .Options;
        return new SubscriptionsDbContext(options, new DesignTimeSqliteConnectionFactory());
    }
}

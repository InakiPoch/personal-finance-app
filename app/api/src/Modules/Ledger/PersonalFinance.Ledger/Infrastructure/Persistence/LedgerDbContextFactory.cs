using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Ledger.Infrastructure.Persistence;

internal sealed class LedgerDbContextFactory : IDesignTimeDbContextFactory<LedgerDbContext> {
    public LedgerDbContext CreateDbContext(string[] args) {
        var connectionString = SqliteConnectionStringHelper.ResolveForDesignTime();
        var options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseSqlite(connectionString, sqlite => sqlite.MigrationsHistoryTable("__EFMigrationsHistory_Ledger"))
            .Options;
        return new LedgerDbContext(options, new DesignTimeSqliteConnectionFactory());
    }
}
